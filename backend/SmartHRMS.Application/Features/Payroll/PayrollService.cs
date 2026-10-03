using System.Globalization;
using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Money;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Payroll;

/// <summary>
/// Payroll workflow: Draft → (calculate) Calculated → (submit, HR/Admin) PendingApproval → (approve, Admin)
/// Approved → (mark paid, Admin) Paid. Draft, Calculated and PendingApproval can be cancelled (HR/Admin).
///
/// Rules:
/// - periods: start before end, at most <see cref="PayrollOptions.MaxPeriodDays"/> days, never overlapping another
///   period that isn't Cancelled; only a Draft period can be edited or deleted;
/// - calculation (Draft or Calculated only) creates or refreshes one record per current employee, in one save, so a
///   failure leaves nothing half-written; manual amounts (overtime, bonus, advance, loan, other) survive recalculation;
/// - employees without a basic salary are skipped and reported, never paid zero silently;
/// - records are editable only while the period is Draft or Calculated; a negative net salary blocks submission;
/// - nobody approves a payroll that contains their own salary;
/// - Employees see only their own records, and only once Approved or Paid; Managers get no payroll access.
/// </summary>
public class PayrollService : IPayrollService
{
    private static readonly IReadOnlyCollection<PayrollPeriodStatus> FinalStatuses = new[] { PayrollPeriodStatus.Approved, PayrollPeriodStatus.Paid };
    private static readonly IReadOnlyCollection<LeaveStatus> ApprovedOnly = new[] { LeaveStatus.Approved };

    private readonly IPayrollRepository _payrollRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly ILeaveRequestRepository _leaveRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly WorkCalendar _calendar;
    private readonly PayrollOptions _options;

    public PayrollService(
        IPayrollRepository payrollRepository,
        IEmployeeRepository employeeRepository,
        IAttendanceRepository attendanceRepository,
        ILeaveRequestRepository leaveRepository,
        IUserRepository userRepository,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        WorkCalendar calendar,
        PayrollOptions options)
    {
        _payrollRepository = payrollRepository;
        _employeeRepository = employeeRepository;
        _attendanceRepository = attendanceRepository;
        _leaveRepository = leaveRepository;
        _userRepository = userRepository;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _calendar = calendar;
        _options = options;
    }

    // ---- periods ----

    public async Task<List<PayrollPeriodDto>> GetPeriodsAsync(PayrollPeriodQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        if (query.Year is < 2000 or > 2100)
        {
            throw new BadRequestException("The year must be between 2000 and 2100.");
        }

        var periods = await _payrollRepository.SearchPeriodsAsync(query.Year, ParseEnum<PayrollPeriodStatus>(query.Status, "payroll status"), cancellationToken);
        return await MapPeriodsAsync(periods, cancellationToken);
    }

    public async Task<PayrollPeriodDto> GetPeriodAsync(Guid id, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        return await MapPeriodAsync(await GetPeriodEntityAsync(id, false, cancellationToken), cancellationToken);
    }

    public async Task<PayrollPeriodDto> CreatePeriodAsync(CreatePayrollPeriodDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var (start, end) = await ValidateRangeAsync(dto, null, cancellationToken);
        var period = new PayrollPeriod
        {
            Name = InputText.Optional(dto.Name) ?? DefaultName(start, end),
            StartDate = start,
            EndDate = end,
            Notes = InputText.Optional(dto.Notes),
            Status = PayrollPeriodStatus.Draft,
            CreatedByUserId = _currentUser.RequireUserId(),
        };

        await _payrollRepository.AddPeriodAsync(period, cancellationToken);
        _auditLogger.Add(AuditActions.PayrollPeriodCreated, nameof(PayrollPeriod), period.Id, $"{period.Name} ({start:yyyy-MM-dd}..{end:yyyy-MM-dd}).");
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return await MapPeriodAsync(period, cancellationToken);
    }

    public async Task<PayrollPeriodDto> UpdatePeriodAsync(Guid id, UpdatePayrollPeriodDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var period = await GetPeriodEntityAsync(id, false, cancellationToken);
        EnsureStatus(period, "edited", PayrollPeriodStatus.Draft);

        var (start, end) = await ValidateRangeAsync(dto, period.Id, cancellationToken);
        period.Name = InputText.Optional(dto.Name) ?? DefaultName(start, end);
        period.StartDate = start;
        period.EndDate = end;
        period.Notes = InputText.Optional(dto.Notes);
        period.UpdatedAt = DateTime.UtcNow;

        _auditLogger.Add(AuditActions.PayrollPeriodUpdated, nameof(PayrollPeriod), period.Id);
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return await MapPeriodAsync(period, cancellationToken);
    }

    public async Task DeletePeriodAsync(Guid id, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var period = await GetPeriodEntityAsync(id, true, cancellationToken);
        EnsureStatus(period, "deleted", PayrollPeriodStatus.Draft);

        foreach (var record in period.Records.ToList())
        {
            _payrollRepository.RemoveRecord(record);
        }

        _payrollRepository.RemovePeriod(period);
        _auditLogger.Add(AuditActions.PayrollPeriodDeleted, nameof(PayrollPeriod), period.Id, period.Name);
        await _payrollRepository.SaveChangesAsync(cancellationToken);
    }

    // ---- calculation ----

    public async Task<PayrollCalculationResultDto> CalculateAsync(Guid periodId, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var period = await GetPeriodEntityAsync(periodId, true, cancellationToken);
        EnsureStatus(period, "calculated", PayrollPeriodStatus.Draft, PayrollPeriodStatus.Calculated);

        if (_calendar.CountWorkingDays(period.StartDate, period.EndDate) == 0)
        {
            throw new BadRequestException("The payroll period contains no working days.");
        }

        var candidates = await _employeeRepository.GetPayrollCandidatesAsync(period.EndDate, cancellationToken);
        var ids = candidates.Select(e => e.Id).ToList();

        var attendance = (await _attendanceRepository.SearchAsync(new AttendanceFilter(null, period.StartDate, period.EndDate, null), cancellationToken))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<DateOnly, AttendanceStatus>)g.ToDictionary(a => a.AttendanceDate, a => a.Status));
        var leave = ids.Count == 0
            ? new Dictionary<Guid, List<LeaveRequest>>()
            : (await _leaveRepository.GetOverlappingAsync(ids, period.StartDate, period.EndDate, ApprovedOnly, cancellationToken))
                .GroupBy(l => l.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

        var existing = period.Records.ToDictionary(r => r.EmployeeId);
        var result = new PayrollCalculationResultDto();
        var calculated = new HashSet<Guid>();

        foreach (var employee in candidates)
        {
            if (employee.BasicSalary is not { } basic)
            {
                result.Skipped.Add(Skip(employee, "No basic salary is set. Add the salary structure, then recalculate."));
                continue;
            }

            var structure = employee.SalaryStructure;
            var salary = new SalaryInput(basic, structure?.HouseRent ?? 0, structure?.MedicalAllowance ?? 0, structure?.TransportAllowance ?? 0, structure?.OtherAllowance ?? 0, structure?.MonthlyTax ?? 0);
            var summary = PayrollCalculator.Summarize(
                _calendar,
                period.StartDate,
                period.EndDate,
                DateOnly.FromDateTime(employee.JoiningDate),
                attendance.GetValueOrDefault(employee.Id) ?? new Dictionary<DateOnly, AttendanceStatus>(),
                leave.GetValueOrDefault(employee.Id) ?? new List<LeaveRequest>());

            if (summary.EmployedWorkingDays == 0)
            {
                result.Skipped.Add(Skip(employee, "Joined after the last working day of the period."));
                continue;
            }

            var amounts = PayrollCalculator.CalculateFixed(salary, summary, _options.DeductRecordedAbsences);

            if (existing.TryGetValue(employee.Id, out var record))
            {
                record.UpdatedAt = DateTime.UtcNow;
                result.Updated++;
            }
            else
            {
                record = new PayrollRecord { PayrollPeriodId = period.Id, EmployeeId = employee.Id };
                await _payrollRepository.AddRecordAsync(record, cancellationToken);
                result.Created++;
            }

            Apply(record, employee, amounts, summary);
            calculated.Add(employee.Id);
        }

        // Employees who left, lost their salary, or are otherwise no longer eligible don't keep a stale record.
        foreach (var stale in period.Records.Where(r => !calculated.Contains(r.EmployeeId)).ToList())
        {
            _payrollRepository.RemoveRecord(stale);
            result.Removed++;
        }

        period.Status = PayrollPeriodStatus.Calculated;
        period.CalculatedAt = DateTime.UtcNow;
        period.CalculatedByUserId = _currentUser.RequireUserId();
        period.UpdatedAt = DateTime.UtcNow;

        _auditLogger.Add(AuditActions.PayrollCalculated, nameof(PayrollPeriod), period.Id,
            $"{result.Created} created, {result.Updated} updated, {result.Removed} removed, {result.Skipped.Count} skipped.");

        // One SaveChanges = one database transaction: all records and the status change, or nothing.
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        result.Period = await MapPeriodAsync(await GetPeriodEntityAsync(period.Id, false, cancellationToken), cancellationToken);
        return result;
    }

    // ---- records ----

    public async Task<List<PayrollRecordDto>> GetRecordsAsync(Guid periodId, PayrollRecordQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        await GetPeriodEntityAsync(periodId, false, cancellationToken);
        var filter = new PayrollRecordFilter(
            periodId,
            null,
            query.DepartmentId,
            query.DesignationId,
            ParseEnum<PayrollRecordStatus>(query.Status, "payroll record status"),
            InputText.Optional(query.Search),
            null);

        var records = await _payrollRepository.SearchRecordsAsync(filter, cancellationToken);
        return records.Select(MapRecord).ToList();
    }

    public async Task<PayrollRecordDto> GetRecordAsync(Guid id, CancellationToken cancellationToken)
    {
        return MapRecord(await GetVisibleRecordAsync(id, cancellationToken));
    }

    public async Task<PayrollRecordDto> UpdateRecordAsync(Guid id, UpdatePayrollRecordDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var record = await _payrollRepository.GetRecordAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Payroll record with id '{id}' was not found.");
        EnsureStatus(record.PayrollPeriod!, "edited", PayrollPeriodStatus.Draft, PayrollPeriodStatus.Calculated);

        record.OvertimeAmount = Money.Round(dto.OvertimeAmount);
        record.Bonus = Money.Round(dto.Bonus);
        record.AdvanceDeduction = Money.Round(dto.AdvanceDeduction);
        record.LoanDeduction = Money.Round(dto.LoanDeduction);
        record.OtherDeduction = Money.Round(dto.OtherDeduction);
        record.Remarks = InputText.Optional(dto.Remarks);
        record.UpdatedAt = DateTime.UtcNow;
        PayrollCalculator.ApplyTotals(record);

        _auditLogger.Add(AuditActions.PayrollRecordUpdated, nameof(PayrollRecord), record.Id, $"Manual amounts of {record.EmployeeCode} changed.");
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return MapRecord(record);
    }

    // ---- workflow ----

    public async Task<PayrollPeriodDto> SubmitAsync(Guid periodId, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var period = await GetPeriodEntityAsync(periodId, true, cancellationToken);
        EnsureStatus(period, "submitted for approval", PayrollPeriodStatus.Calculated);

        if (period.Records.Count == 0)
        {
            throw new BadRequestException("This payroll has no records. Add salaries and recalculate before submitting.");
        }

        var needsReview = period.Records.Count(r => r.Status == PayrollRecordStatus.NeedsReview);
        if (needsReview > 0)
        {
            throw new BadRequestException($"{needsReview} payroll record(s) need review (e.g. a negative net salary). Fix them before submitting.");
        }

        period.Status = PayrollPeriodStatus.PendingApproval;
        period.SubmittedAt = DateTime.UtcNow;
        period.SubmittedByUserId = _currentUser.RequireUserId();
        period.UpdatedAt = DateTime.UtcNow;

        _auditLogger.Add(AuditActions.PayrollSubmitted, nameof(PayrollPeriod), period.Id);
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return await MapPeriodAsync(period, cancellationToken);
    }

    public async Task<PayrollPeriodDto> ApproveAsync(Guid periodId, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var period = await GetPeriodEntityAsync(periodId, true, cancellationToken);
        EnsureStatus(period, "approved", PayrollPeriodStatus.PendingApproval);

        if (_currentUser.EmployeeId is { } own && period.Records.Any(r => r.EmployeeId == own))
        {
            throw new ForbiddenException("This payroll includes your own salary, so another Admin must approve it.");
        }

        if (period.Records.Count == 0 || period.Records.Any(r => r.Status == PayrollRecordStatus.NeedsReview))
        {
            throw new BadRequestException("This payroll has no records or has records that need review.");
        }

        period.Status = PayrollPeriodStatus.Approved;
        period.ApprovedAt = DateTime.UtcNow;
        period.ApprovedByUserId = _currentUser.RequireUserId();
        period.UpdatedAt = DateTime.UtcNow;
        SetRecordStatus(period, PayrollRecordStatus.Approved);

        _auditLogger.Add(AuditActions.PayrollApproved, nameof(PayrollPeriod), period.Id);
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return await MapPeriodAsync(period, cancellationToken);
    }

    public async Task<PayrollPeriodDto> MarkPaidAsync(Guid periodId, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var period = await GetPeriodEntityAsync(periodId, true, cancellationToken);
        EnsureStatus(period, "marked as paid", PayrollPeriodStatus.Approved);

        period.Status = PayrollPeriodStatus.Paid;
        period.PaidAt = DateTime.UtcNow;
        period.PaidByUserId = _currentUser.RequireUserId();
        period.UpdatedAt = DateTime.UtcNow;
        SetRecordStatus(period, PayrollRecordStatus.Paid);

        _auditLogger.Add(AuditActions.PayrollPaid, nameof(PayrollPeriod), period.Id);
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return await MapPeriodAsync(period, cancellationToken);
    }

    public async Task<PayrollPeriodDto> CancelAsync(Guid periodId, CancelPayrollDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var period = await GetPeriodEntityAsync(periodId, true, cancellationToken);
        EnsureStatus(period, "cancelled", PayrollPeriodStatus.Draft, PayrollPeriodStatus.Calculated, PayrollPeriodStatus.PendingApproval);

        period.Status = PayrollPeriodStatus.Cancelled;
        period.CancelledAt = DateTime.UtcNow;
        period.CancelledByUserId = _currentUser.RequireUserId();
        period.UpdatedAt = DateTime.UtcNow;
        var reason = InputText.Optional(dto.Reason);
        if (reason is not null)
        {
            period.Notes = period.Notes is null ? $"Cancelled: {reason}" : $"{period.Notes}\nCancelled: {reason}";
        }

        SetRecordStatus(period, PayrollRecordStatus.Cancelled);

        _auditLogger.Add(AuditActions.PayrollCancelled, nameof(PayrollPeriod), period.Id, reason);
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return await MapPeriodAsync(period, cancellationToken);
    }

    // ---- employee self-service ----

    public async Task<List<PayrollRecordDto>> GetEmployeeHistoryAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        _currentUser.EnsureSelfOrHrOrAdmin(employeeId);
        await _employeeRepository.EnsureExistsAsync(employeeId, cancellationToken);

        // Employees only ever see final payroll; HR/Admin also see work in progress.
        var statuses = _currentUser.IsHrOrAdmin() ? null : FinalStatuses;
        var records = await _payrollRepository.SearchRecordsAsync(new PayrollRecordFilter(null, employeeId, null, null, null, null, statuses), cancellationToken);
        return records.Select(MapRecord).ToList();
    }

    public async Task<PayslipDto> GetPayslipAsync(Guid recordId, CancellationToken cancellationToken)
    {
        var record = await GetVisibleRecordAsync(recordId, cancellationToken);
        var period = record.PayrollPeriod!;
        var names = await GetNamesAsync(new[] { period.ApprovedByUserId }, cancellationToken);

        _auditLogger.Add(AuditActions.PayslipViewed, nameof(PayrollRecord), record.Id, $"Payslip of {record.EmployeeCode}, {period.Name}.");
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return new PayslipDto
        {
            RecordId = record.Id,
            CompanyName = _options.CompanyName,
            CompanyAddress = _options.CompanyAddress,
            Currency = _options.Currency,
            EmployeeId = record.EmployeeId,
            EmployeeCode = record.EmployeeCode,
            EmployeeName = record.EmployeeName,
            DepartmentName = record.DepartmentName,
            DesignationName = record.DesignationName,
            PeriodName = period.Name,
            PeriodStartDate = period.StartDate,
            PeriodEndDate = period.EndDate,
            Earnings = Lines(
                ("Basic salary", record.BasicSalary, true),
                ("House rent", record.HouseRent, false),
                ("Medical allowance", record.MedicalAllowance, false),
                ("Transport allowance", record.TransportAllowance, false),
                ("Other allowance", record.OtherAllowance, false),
                ("Overtime", record.OvertimeAmount, false),
                ("Bonus", record.Bonus, false)),
            Deductions = Lines(
                ("Tax", record.Tax, false),
                ("Unpaid leave", record.LeaveDeduction, false),
                ("Advance", record.AdvanceDeduction, false),
                ("Loan", record.LoanDeduction, false),
                ("Other deduction", record.OtherDeduction, false)),
            GrossSalary = record.GrossSalary,
            TotalDeduction = record.TotalDeduction,
            NetSalary = record.NetSalary,
            WorkingDays = record.WorkingDays,
            PresentDays = record.PresentDays,
            PaidLeaveDays = record.PaidLeaveDays,
            UnpaidLeaveDays = record.UnpaidLeaveDays,
            AbsentDays = record.AbsentDays,
            PaymentStatus = period.Status.ToString(),
            IsFinal = FinalStatuses.Contains(period.Status),
            ApprovedBy = NameOf(names, period.ApprovedByUserId),
            ApprovedAt = period.ApprovedAt,
            PaidAt = period.PaidAt,
            Remarks = record.Remarks,
        };
    }

    // ---- helpers ----

    /// <summary>
    /// HR/Admin see every record. An employee sees their own record once the payroll is Approved or Paid; anything
    /// else is reported as not found, so record ids of other employees reveal nothing.
    /// </summary>
    private async Task<PayrollRecord> GetVisibleRecordAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await _payrollRepository.GetRecordAsync(id, cancellationToken);
        var visible = record is not null
            && (_currentUser.IsHrOrAdmin() || (_currentUser.IsSelf(record.EmployeeId) && FinalStatuses.Contains(record.PayrollPeriod!.Status)));

        return visible ? record! : throw new NotFoundException($"Payroll record with id '{id}' was not found.");
    }

    private async Task<PayrollPeriod> GetPeriodEntityAsync(Guid id, bool includeRecords, CancellationToken cancellationToken)
    {
        return await _payrollRepository.GetPeriodAsync(id, includeRecords, cancellationToken)
            ?? throw new NotFoundException($"Payroll period with id '{id}' was not found.");
    }

    private static void EnsureStatus(PayrollPeriod period, string action, params PayrollPeriodStatus[] allowed)
    {
        if (!allowed.Contains(period.Status))
        {
            throw new ConflictException($"A {period.Status} payroll can't be {action}. Allowed when: {string.Join(", ", allowed)}.");
        }
    }

    private async Task<(DateOnly Start, DateOnly End)> ValidateRangeAsync(CreatePayrollPeriodDto dto, Guid? excludeId, CancellationToken cancellationToken)
    {
        var start = dto.StartDate!.Value;
        var end = dto.EndDate!.Value;

        if (start >= end)
        {
            throw new BadRequestException("The start date must be before the end date.");
        }

        if (start.Year is < 2000 or > 2100 || end.Year is < 2000 or > 2100)
        {
            throw new BadRequestException("Payroll dates must be between the years 2000 and 2100.");
        }

        if (end.DayNumber - start.DayNumber + 1 > _options.MaxPeriodDays)
        {
            throw new BadRequestException($"A payroll period can cover at most {_options.MaxPeriodDays} days.");
        }

        if (_calendar.CountWorkingDays(start, end) == 0)
        {
            throw new BadRequestException("The payroll period contains no working days.");
        }

        if (await _payrollRepository.HasOverlappingPeriodAsync(start, end, excludeId, cancellationToken))
        {
            throw new ConflictException($"Another payroll period already covers dates between {start:yyyy-MM-dd} and {end:yyyy-MM-dd}.");
        }

        return (start, end);
    }

    private static string DefaultName(DateOnly start, DateOnly end)
    {
        var isFullMonth = start.Day == 1 && end == start.AddMonths(1).AddDays(-1);
        return isFullMonth
            ? start.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd}";
    }

    private static void Apply(PayrollRecord record, Employee employee, FixedAmounts amounts, AttendanceSummary summary)
    {
        record.EmployeeCode = employee.EmployeeCode;
        record.EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim();
        record.DepartmentName = employee.Department?.Name;
        record.DesignationName = employee.Designation?.Name;

        record.BasicSalary = amounts.BasicSalary;
        record.HouseRent = amounts.HouseRent;
        record.MedicalAllowance = amounts.MedicalAllowance;
        record.TransportAllowance = amounts.TransportAllowance;
        record.OtherAllowance = amounts.OtherAllowance;
        record.Tax = amounts.Tax;
        record.LeaveDeduction = amounts.LeaveDeduction;

        record.WorkingDays = summary.EmployedWorkingDays;
        record.PresentDays = summary.PresentDays;
        record.PaidLeaveDays = summary.PaidLeaveDays;
        record.UnpaidLeaveDays = summary.UnpaidLeaveDays;
        record.AbsentDays = summary.AbsentDays;

        PayrollCalculator.ApplyTotals(record);
    }

    private static void SetRecordStatus(PayrollPeriod period, PayrollRecordStatus status)
    {
        foreach (var record in period.Records)
        {
            record.Status = status;
            record.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static SkippedEmployeeDto Skip(Employee employee, string reason)
    {
        return new SkippedEmployeeDto
        {
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
            Reason = reason,
        };
    }

    private static List<PayslipLineDto> Lines(params (string Label, decimal Amount, bool Always)[] lines)
    {
        return lines.Where(l => l.Always || l.Amount != 0).Select(l => new PayslipLineDto { Label = l.Label, Amount = l.Amount }).ToList();
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string label) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out _) || !Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new BadRequestException($"'{value}' is not a valid {label}. Allowed values: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        return parsed;
    }

    private async Task<Dictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.OfType<Guid>().Distinct().ToList();
        return distinct.Count == 0 ? new Dictionary<Guid, string>() : await _userRepository.GetDisplayNamesAsync(distinct, cancellationToken);
    }

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id)
    {
        return id is { } value && names.TryGetValue(value, out var name) ? name : null;
    }

    private async Task<PayrollPeriodDto> MapPeriodAsync(PayrollPeriod period, CancellationToken cancellationToken)
    {
        return (await MapPeriodsAsync(new[] { period }, cancellationToken))[0];
    }

    private async Task<List<PayrollPeriodDto>> MapPeriodsAsync(IReadOnlyCollection<PayrollPeriod> periods, CancellationToken cancellationToken)
    {
        var totals = periods.Count == 0
            ? new Dictionary<Guid, PayrollTotals>()
            : await _payrollRepository.GetTotalsAsync(periods.Select(p => p.Id).ToList(), cancellationToken);
        var names = await GetNamesAsync(
            periods.SelectMany(p => new[] { p.CreatedByUserId, p.CalculatedByUserId, p.SubmittedByUserId, p.ApprovedByUserId, p.PaidByUserId, p.CancelledByUserId }),
            cancellationToken);

        return periods.Select(period =>
        {
            var total = totals.GetValueOrDefault(period.Id) ?? new PayrollTotals(0, 0, 0, 0, 0);
            return new PayrollPeriodDto
            {
                Id = period.Id,
                Name = period.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                Status = period.Status.ToString(),
                Notes = period.Notes,
                WorkingDays = _calendar.CountWorkingDays(period.StartDate, period.EndDate),
                EmployeeCount = total.EmployeeCount,
                GrossTotal = total.GrossTotal,
                DeductionTotal = total.DeductionTotal,
                NetTotal = total.NetTotal,
                NeedsReviewCount = total.NeedsReviewCount,
                CreatedBy = NameOf(names, period.CreatedByUserId),
                CreatedAt = period.CreatedAt,
                UpdatedAt = period.UpdatedAt,
                CalculatedAt = period.CalculatedAt,
                CalculatedBy = NameOf(names, period.CalculatedByUserId),
                SubmittedAt = period.SubmittedAt,
                SubmittedBy = NameOf(names, period.SubmittedByUserId),
                ApprovedAt = period.ApprovedAt,
                ApprovedBy = NameOf(names, period.ApprovedByUserId),
                PaidAt = period.PaidAt,
                PaidBy = NameOf(names, period.PaidByUserId),
                CancelledAt = period.CancelledAt,
                CancelledBy = NameOf(names, period.CancelledByUserId),
                Actions = ActionsFor(period, total),
            };
        }).ToList();
    }

    private PayrollActionsDto ActionsFor(PayrollPeriod period, PayrollTotals totals)
    {
        var hr = _currentUser.IsHrOrAdmin();
        var admin = _currentUser.IsAdmin();
        var status = period.Status;

        return new PayrollActionsDto
        {
            CanEdit = hr && status == PayrollPeriodStatus.Draft,
            CanDelete = hr && status == PayrollPeriodStatus.Draft,
            CanCalculate = hr && status is PayrollPeriodStatus.Draft or PayrollPeriodStatus.Calculated,
            CanEditRecords = hr && status is PayrollPeriodStatus.Draft or PayrollPeriodStatus.Calculated,
            CanSubmit = hr && status == PayrollPeriodStatus.Calculated && totals.EmployeeCount > 0 && totals.NeedsReviewCount == 0,
            CanApprove = admin && status == PayrollPeriodStatus.PendingApproval,
            CanMarkPaid = admin && status == PayrollPeriodStatus.Approved,
            CanCancel = hr && status is PayrollPeriodStatus.Draft or PayrollPeriodStatus.Calculated or PayrollPeriodStatus.PendingApproval,
        };
    }

    private PayrollRecordDto MapRecord(PayrollRecord record)
    {
        var period = record.PayrollPeriod;
        return new PayrollRecordDto
        {
            Id = record.Id,
            PayrollPeriodId = record.PayrollPeriodId,
            PeriodName = period?.Name ?? string.Empty,
            PeriodStartDate = period?.StartDate ?? default,
            PeriodEndDate = period?.EndDate ?? default,
            PeriodStatus = period?.Status.ToString() ?? string.Empty,
            EmployeeId = record.EmployeeId,
            EmployeeCode = record.EmployeeCode,
            EmployeeName = record.EmployeeName,
            DepartmentName = record.DepartmentName,
            DesignationName = record.DesignationName,
            BasicSalary = record.BasicSalary,
            HouseRent = record.HouseRent,
            MedicalAllowance = record.MedicalAllowance,
            TransportAllowance = record.TransportAllowance,
            OtherAllowance = record.OtherAllowance,
            OvertimeAmount = record.OvertimeAmount,
            Bonus = record.Bonus,
            GrossSalary = record.GrossSalary,
            Tax = record.Tax,
            LeaveDeduction = record.LeaveDeduction,
            AdvanceDeduction = record.AdvanceDeduction,
            LoanDeduction = record.LoanDeduction,
            OtherDeduction = record.OtherDeduction,
            TotalDeduction = record.TotalDeduction,
            NetSalary = record.NetSalary,
            WorkingDays = record.WorkingDays,
            PresentDays = record.PresentDays,
            PaidLeaveDays = record.PaidLeaveDays,
            UnpaidLeaveDays = record.UnpaidLeaveDays,
            AbsentDays = record.AbsentDays,
            Status = record.Status.ToString(),
            Remarks = record.Remarks,
            CanEdit = _currentUser.IsHrOrAdmin() && period?.Status is PayrollPeriodStatus.Draft or PayrollPeriodStatus.Calculated,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
        };
    }
}
