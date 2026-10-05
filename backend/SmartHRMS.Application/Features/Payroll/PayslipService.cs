using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Payroll;

public interface IPayslipService
{
    /// <summary>Every payroll record (any status) with its payslip and payment, paged and filtered (HR/Admin).</summary>
    Task<PagedResult<PayrollRecordDto>> GetHistoryAsync(PayrollHistoryQueryDto query, CancellationToken cancellationToken);

    /// <summary>Issued payslips only, paged and filtered (HR/Admin).</summary>
    Task<PagedResult<PayrollRecordDto>> GetPayslipsAsync(PayrollHistoryQueryDto query, CancellationToken cancellationToken);

    /// <summary>One employee's payslips (HR/Admin, or the employee themself).</summary>
    Task<PagedResult<PayrollRecordDto>> GetEmployeePayslipsAsync(Guid employeeId, PayrollHistoryQueryDto query, CancellationToken cancellationToken);

    /// <summary>The signed-in employee's payslips (employee from the token).</summary>
    Task<PagedResult<PayrollRecordDto>> GetMyPayslipsAsync(PayrollHistoryQueryDto query, CancellationToken cancellationToken);

    /// <summary>The signed-in employee's paid payslips, newest payment first.</summary>
    Task<PagedResult<PayrollRecordDto>> GetMyPaymentsAsync(PayrollHistoryQueryDto query, CancellationToken cancellationToken);

    /// <summary>The signed-in employee's latest payslip (404 when none has been issued yet).</summary>
    Task<PayslipDto> GetMyCurrentPayslipAsync(CancellationToken cancellationToken);

    /// <summary>A payslip by its id: the owner or HR/Admin; anyone else gets 404.</summary>
    Task<PayslipDto> GetByIdAsync(Guid payslipId, CancellationToken cancellationToken);

    /// <summary>
    /// The payslip of a payroll record (Day 16 route): HR/Admin any record (a preview before approval), an employee
    /// only their own issued payslip.
    /// </summary>
    Task<PayslipDto> GetByRecordAsync(Guid recordId, CancellationToken cancellationToken);

    /// <summary>Issues the missing payslips of an Approved or Paid payroll (HR/Admin). Never for unapproved payroll.</summary>
    Task<PayslipGenerationResultDto> GenerateAsync(Guid periodId, CancellationToken cancellationToken);
}

/// <summary>
/// Payslips are the approved payroll snapshot: issued at approval (or by <see cref="GenerateAsync"/> for older approved
/// payroll), never for Draft, Calculated, PendingApproval or Cancelled payroll. Amounts come from the locked payroll
/// record; nothing is recalculated here. Payment is made through payment batches (Day 18) and mirrored here.
///
/// Access: HR/Admin see everything. Employees reach only their own payslips, and the "my" endpoints take the
/// employee from the token, never from the request. Managers have no access to their reports' payslips.
/// </summary>
public class PayslipService : IPayslipService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly IPayrollRepository _payrollRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly PayrollOptions _options;
    private readonly AttendanceClock _clock;

    public PayslipService(
        IPayrollRepository payrollRepository,
        IEmployeeRepository employeeRepository,
        IUserRepository userRepository,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        PayrollOptions options,
        AttendanceClock clock)
    {
        _payrollRepository = payrollRepository;
        _employeeRepository = employeeRepository;
        _userRepository = userRepository;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _options = options;
        _clock = clock;
    }

    // ---- lists ----

    public Task<PagedResult<PayrollRecordDto>> GetHistoryAsync(PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        return SearchAsync(query, query.EmployeeId, onlyWithPayslip: false, onlyPaid: false, cancellationToken);
    }

    public Task<PagedResult<PayrollRecordDto>> GetPayslipsAsync(PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        return SearchAsync(query, query.EmployeeId, onlyWithPayslip: true, onlyPaid: false, cancellationToken);
    }

    public async Task<PagedResult<PayrollRecordDto>> GetEmployeePayslipsAsync(Guid employeeId, PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureSelfOrHrOrAdmin(employeeId);
        await _employeeRepository.EnsureExistsAsync(employeeId, cancellationToken);
        return await SearchAsync(query, employeeId, onlyWithPayslip: true, onlyPaid: false, cancellationToken);
    }

    public Task<PagedResult<PayrollRecordDto>> GetMyPayslipsAsync(PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        return SearchAsync(query, _currentUser.RequireEmployeeId(), onlyWithPayslip: true, onlyPaid: false, cancellationToken);
    }

    public Task<PagedResult<PayrollRecordDto>> GetMyPaymentsAsync(PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        query.SortBy ??= nameof(PayrollHistorySort.PaymentDate);
        return SearchAsync(query, _currentUser.RequireEmployeeId(), onlyWithPayslip: true, onlyPaid: true, cancellationToken);
    }

    // ---- single payslips ----

    public async Task<PayslipDto> GetMyCurrentPayslipAsync(CancellationToken cancellationToken)
    {
        var employeeId = _currentUser.RequireEmployeeId();
        var filter = new PayrollHistoryFilter(employeeId, null, null, null, null, null, null, OnlyWithPayslip: true);
        var (items, _) = await _payrollRepository.SearchHistoryAsync(filter, new PageRequest(1, 1, PayrollHistorySort.Period, Descending: true), cancellationToken);
        var latest = items.FirstOrDefault()?.Payslip
            ?? throw new NotFoundException("No payslip has been issued to you yet.");

        return await GetByIdAsync(latest.Id, cancellationToken);
    }

    public async Task<PayslipDto> GetByIdAsync(Guid payslipId, CancellationToken cancellationToken)
    {
        var payslip = await _payrollRepository.GetPayslipAsync(payslipId, cancellationToken);
        if (payslip is null || (!_currentUser.IsHrOrAdmin() && !_currentUser.IsSelf(payslip.EmployeeId)))
        {
            // Not 403: another employee's payslip id must reveal nothing.
            throw new NotFoundException($"Payslip with id '{payslipId}' was not found.");
        }

        return await BuildAsync(payslip.PayrollRecord!, cancellationToken);
    }

    public async Task<PayslipDto> GetByRecordAsync(Guid recordId, CancellationToken cancellationToken)
    {
        var record = await _payrollRepository.GetRecordAsync(recordId, cancellationToken);
        var visible = record is not null && (_currentUser.IsHrOrAdmin() || (_currentUser.IsSelf(record.EmployeeId) && record.Payslip is not null));
        if (!visible)
        {
            throw new NotFoundException($"Payroll record with id '{recordId}' was not found.");
        }

        return await BuildAsync(record!, cancellationToken);
    }

    // ---- generation and payment ----

    public async Task<PayslipGenerationResultDto> GenerateAsync(Guid periodId, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var period = await _payrollRepository.GetPeriodAsync(periodId, includeRecords: true, cancellationToken)
            ?? throw new NotFoundException($"Payroll period with id '{periodId}' was not found.");

        var existing = period.Records.Count(r => r.Payslip is not null);
        var issued = PayslipIssuer.IssueMissing(period, _currentUser.RequireUserId(), DateTime.UtcNow);
        foreach (var payslip in issued)
        {
            await _payrollRepository.AddPayslipAsync(payslip, cancellationToken);
        }

        if (issued.Count > 0)
        {
            _auditLogger.Add(AuditActions.PayslipsGenerated, nameof(PayrollPeriod), period.Id, $"{issued.Count} payslips issued.");
            await _payrollRepository.SaveChangesAsync(cancellationToken);
        }

        return new PayslipGenerationResultDto { PayrollPeriodId = period.Id, Generated = issued.Count, AlreadyGenerated = existing };
    }

    // ---- helpers ----

    private async Task<PagedResult<PayrollRecordDto>> SearchAsync(PayrollHistoryQueryDto query, Guid? employeeId, bool onlyWithPayslip, bool onlyPaid, CancellationToken cancellationToken)
    {
        if (query.Month is < 1 or > 12)
        {
            throw new BadRequestException("The month must be between 1 and 12.");
        }

        if (query.Year is < 2000 or > 2100)
        {
            throw new BadRequestException("The year must be between 2000 and 2100.");
        }

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            throw new BadRequestException($"The page must be 1 or more and the pageSize between 1 and {MaxPageSize}.");
        }

        var sort = ParseEnum<PayrollHistorySort>(query.SortBy, "sortBy") ?? PayrollHistorySort.Period;
        var direction = InputText.Optional(query.SortDirection)?.ToLowerInvariant();
        if (direction is not (null or "asc" or "desc"))
        {
            throw new BadRequestException($"'{query.SortDirection}' is not a valid sortDirection. Allowed values: asc, desc.");
        }

        var descending = direction is null ? sort != PayrollHistorySort.Employee : direction == "desc";
        var paymentStatus = onlyPaid ? PaymentStatus.Paid : ParseEnum<PaymentStatus>(query.PaymentStatus, "payment status");

        var filter = new PayrollHistoryFilter(
            employeeId,
            query.DepartmentId,
            query.Year,
            query.Month,
            ParseEnum<PayrollPeriodStatus>(query.Status, "payroll status"),
            paymentStatus,
            InputText.Optional(query.Search),
            onlyWithPayslip || paymentStatus is not null);

        var (items, total) = await _payrollRepository.SearchHistoryAsync(filter, new PageRequest(page, pageSize, sort, descending), cancellationToken);
        var isHr = _currentUser.IsHrOrAdmin();

        return new PagedResult<PayrollRecordDto>
        {
            Items = items.Select(record => PayrollService.ToDto(record, isHr)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    private async Task<PayslipDto> BuildAsync(PayrollRecord record, CancellationToken cancellationToken)
    {
        var period = record.PayrollPeriod!;
        var payslip = record.Payslip;
        var employee = await _employeeRepository.GetByIdAsync(record.EmployeeId, cancellationToken);
        var approverIds = new[] { period.ApprovedByUserId }.OfType<Guid>().ToList();
        var names = approverIds.Count == 0 ? new Dictionary<Guid, string>() : await _userRepository.GetDisplayNamesAsync(approverIds, cancellationToken);

        _auditLogger.Add(AuditActions.PayslipViewed, payslip is null ? nameof(PayrollRecord) : nameof(Payslip), payslip?.Id ?? record.Id,
            $"Payslip of {record.EmployeeCode}, {period.Name}.");
        await _payrollRepository.SaveChangesAsync(cancellationToken);

        return new PayslipDto
        {
            PayslipId = payslip?.Id,
            PayslipNumber = payslip?.PayslipNumber,
            RecordId = record.Id,
            PayrollPeriodId = period.Id,
            CompanyName = _options.CompanyName,
            CompanyAddress = _options.CompanyAddress,
            Currency = _options.Currency,
            EmployeeId = record.EmployeeId,
            EmployeeCode = record.EmployeeCode,
            EmployeeName = record.EmployeeName,
            EmployeePhotoUrl = employee?.PhotoUrl,
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
                ("Provident fund", record.ProvidentFund, false),
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
            PayrollStatus = period.Status.ToString(),
            PaymentStatus = (payslip?.PaymentStatus ?? PaymentStatus.Unpaid).ToString(),
            PaymentDate = payslip?.PaymentDate,
            PaymentMethod = payslip?.PaymentMethod?.ToString(),
            PaymentReference = payslip?.PaymentReference,
            GeneratedAt = payslip?.GeneratedAt,
            IsFinal = payslip is not null,
            ApprovedBy = period.ApprovedByUserId is { } approver && names.TryGetValue(approver, out var name) ? name : null,
            ApprovedAt = period.ApprovedAt,
            PaidAt = payslip?.PaidAt,
            Remarks = record.Remarks,
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
}
