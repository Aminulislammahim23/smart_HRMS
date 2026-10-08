using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Export;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Application.Features.PayrollReports.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.PayrollReports;

public enum PayrollReportGroupBy
{
    Period,
    Department,
}

/// <summary>The exportable payroll reports.</summary>
public enum PayrollReportKind
{
    Periods,
    Summary,
    Departments,
    Employees,
    Deductions,
    Allowances,
}

public interface IPayrollReportService
{
    /// <summary>Payroll history at period level, with totals (HR/Admin).</summary>
    Task<PagedResult<PayrollPeriodHistoryDto>> GetPeriodHistoryAsync(PayrollPeriodHistoryQueryDto query, CancellationToken cancellationToken);

    /// <summary>Totals plus one row per payroll period or per department (HR/Admin).</summary>
    Task<PayrollReportDto> GetReportAsync(PayrollReportQueryDto query, PayrollReportGroupBy defaultGroupBy, CancellationToken cancellationToken);

    /// <summary>Employee payroll report: one row per employee and period, paged (HR/Admin).</summary>
    Task<PagedResult<PayrollRecordDto>> GetEmployeeReportAsync(PayrollReportQueryDto query, CancellationToken cancellationToken);

    /// <summary>A report as CSV or XLSX (HR/Admin); the export is written to the audit log.</summary>
    Task<ExportFile> ExportAsync(PayrollReportKind kind, PayrollReportQueryDto query, string? format, CancellationToken cancellationToken);
}

/// <summary>
/// Payroll reporting (Day 19). Amounts are the stored payroll record values from the Day 16 calculation; nothing is
/// recalculated. Only HR and Admin can read reports: employees see their own payslips, and managers have no access to
/// their reports' salaries (Day 16 rule), so a report never contains salary data the caller may not see.
/// </summary>
public class PayrollReportService : IPayrollReportService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    /// <summary>Largest employee report export; narrower filters are needed beyond it.</summary>
    public const int MaxExportRows = 10_000;

    private readonly IPayrollReportRepository _repository;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;

    public PayrollReportService(IPayrollReportRepository repository, IAuditLogger auditLogger, ICurrentUser currentUser)
    {
        _repository = repository;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<PayrollPeriodHistoryDto>> GetPeriodHistoryAsync(PayrollPeriodHistoryQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        var (page, pageSize) = Paging(query.Page, query.PageSize);
        var (periods, total) = await _repository.SearchPeriodsAsync(PeriodFilter(query), page, pageSize, cancellationToken);
        return new PagedResult<PayrollPeriodHistoryDto>
        {
            Items = await WithTotalsAsync(periods, cancellationToken),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    public async Task<PayrollReportDto> GetReportAsync(PayrollReportQueryDto query, PayrollReportGroupBy defaultGroupBy, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        var filter = ReportFilter(query);
        var groupBy = ParseEnum<PayrollReportGroupBy>(query.GroupBy, "groupBy") ?? defaultGroupBy;
        return await BuildReportAsync(filter, groupBy, cancellationToken);
    }

    public async Task<PagedResult<PayrollRecordDto>> GetEmployeeReportAsync(PayrollReportQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        var filter = ReportFilter(query);
        var (page, pageSize) = Paging(query.Page, query.PageSize);
        var (records, total) = await _repository.SearchRecordsAsync(filter, page, pageSize, cancellationToken);
        return new PagedResult<PayrollRecordDto>
        {
            Items = records.Select(r => PayrollService.ToDto(r, isHrOrAdmin: true)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    public async Task<ExportFile> ExportAsync(PayrollReportKind kind, PayrollReportQueryDto query, string? format, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        var exportFormat = TableExporter.ParseFormat(format);

        ExportTable table;
        switch (kind)
        {
            case PayrollReportKind.Periods:
                table = PeriodsTable(await AllPeriodsAsync(query, cancellationToken));
                break;
            case PayrollReportKind.Employees:
                table = await EmployeesTableAsync(ReportFilter(query), cancellationToken);
                break;
            default:
                var defaultGroup = kind == PayrollReportKind.Departments ? PayrollReportGroupBy.Department : PayrollReportGroupBy.Period;
                var groupBy = kind is PayrollReportKind.Deductions or PayrollReportKind.Allowances
                    ? ParseEnum<PayrollReportGroupBy>(query.GroupBy, "groupBy") ?? defaultGroup
                    : defaultGroup;
                var report = await BuildReportAsync(ReportFilter(query), groupBy, cancellationToken);
                table = kind switch
                {
                    PayrollReportKind.Deductions => DeductionsTable(report),
                    PayrollReportKind.Allowances => AllowancesTable(report),
                    PayrollReportKind.Departments => DepartmentsTable(report),
                    _ => SummaryTable(report),
                };
                break;
        }

        var name = $"payroll-{kind.ToString().ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd-HHmm}";
        // What was exported and how many rows; never amounts.
        await _auditLogger.WriteAsync(AuditActions.PayrollReportExported, "PayrollReport", query.PayrollPeriodId,
            $"{kind} report exported as {exportFormat.ToString().ToLowerInvariant()} ({table.Rows.Count} rows){FilterText(query)}.",
            _currentUser.UserId, _currentUser.Username, cancellationToken);

        return TableExporter.Write(table, exportFormat, name);
    }

    // ---- building ----

    private async Task<PayrollReportDto> BuildReportAsync(PayrollReportFilter filter, PayrollReportGroupBy groupBy, CancellationToken cancellationToken)
    {
        var byPeriod = await _repository.SumByPeriodAsync(filter, cancellationToken);
        var totals = Combine(byPeriod.Select(Amounts), await _repository.CountEmployeesAsync(filter, cancellationToken));

        List<PayrollReportRowDto> rows;
        if (groupBy == PayrollReportGroupBy.Department)
        {
            rows = (await _repository.SumByDepartmentAsync(filter, cancellationToken))
                .Select(row => Row(row, row.Key ?? "Unassigned"))
                .OrderBy(row => row.Label)
                .ToList();
        }
        else
        {
            var periods = (await _repository.GetPeriodsAsync(byPeriod.Select(r => r.Key).ToList(), cancellationToken)).ToDictionary(p => p.Id);
            rows = byPeriod
                .Where(row => periods.ContainsKey(row.Key))
                .Select(row =>
                {
                    var period = periods[row.Key];
                    var dto = Row(row, period.Name);
                    dto.PayrollPeriodId = period.Id;
                    dto.PeriodStartDate = period.StartDate;
                    dto.PeriodEndDate = period.EndDate;
                    dto.PeriodStatus = period.Status.ToString();
                    return dto;
                })
                .OrderByDescending(row => row.PeriodStartDate)
                .ToList();
        }

        return new PayrollReportDto
        {
            GroupBy = groupBy.ToString().ToLowerInvariant(),
            Statuses = (filter.Statuses ?? Enum.GetValues<PayrollPeriodStatus>()).Select(s => s.ToString()).ToList(),
            Totals = totals,
            Rows = rows,
        };
    }

    private async Task<List<PayrollPeriodHistoryDto>> WithTotalsAsync(List<PayrollPeriod> periods, CancellationToken cancellationToken)
    {
        var filter = new PayrollReportFilter(null, null, null, null, null, null, null, null, null, null, periods.Select(p => p.Id).ToList());
        var sums = (await _repository.SumByPeriodAsync(filter, cancellationToken)).ToDictionary(r => r.Key);
        return periods.Select(period =>
        {
            var dto = new PayrollPeriodHistoryDto
            {
                Id = period.Id,
                Name = period.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                Month = period.StartDate.Month,
                Year = period.StartDate.Year,
                Status = period.Status.ToString(),
                IsLocked = period.IsLocked,
                CreatedAt = period.CreatedAt,
                CalculatedAt = period.CalculatedAt,
                ApprovedAt = period.ApprovedAt,
                FinalizedAt = period.FinalizedAt,
                PaidAt = period.PaidAt,
                CancelledAt = period.CancelledAt,
            };
            if (sums.TryGetValue(period.Id, out var sum))
            {
                Copy(Amounts(sum), dto);
            }

            return dto;
        }).ToList();
    }

    private async Task<List<PayrollPeriodHistoryDto>> AllPeriodsAsync(PayrollReportQueryDto query, CancellationToken cancellationToken)
    {
        var historyQuery = new PayrollPeriodHistoryQueryDto { Search = query.Search, Month = query.Month, Year = query.Year, Status = query.Status, From = query.From, To = query.To };
        var (periods, _) = await _repository.SearchPeriodsAsync(PeriodFilter(historyQuery), 1, MaxExportRows, cancellationToken);
        return await WithTotalsAsync(periods, cancellationToken);
    }

    private static PayrollAmountsDto Amounts<TKey>(PayrollAmountsRow<TKey> row) => new()
    {
        EmployeeCount = row.EmployeeCount,
        RecordCount = row.RecordCount,
        BasicSalary = row.BasicSalary,
        HouseRent = row.HouseRent,
        MedicalAllowance = row.MedicalAllowance,
        TransportAllowance = row.TransportAllowance,
        OtherAllowance = row.OtherAllowance,
        TotalAllowances = row.HouseRent + row.MedicalAllowance + row.TransportAllowance + row.OtherAllowance,
        Overtime = row.Overtime,
        Bonus = row.Bonus,
        GrossSalary = row.GrossSalary,
        Tax = row.Tax,
        ProvidentFund = row.ProvidentFund,
        LeaveDeduction = row.LeaveDeduction,
        AdvanceDeduction = row.AdvanceDeduction,
        LoanDeduction = row.LoanDeduction,
        OtherDeduction = row.OtherDeduction,
        OtherDeductions = row.TotalDeduction - row.Tax,
        TotalDeduction = row.TotalDeduction,
        NetSalary = row.NetSalary,
        PaidNetSalary = row.PaidNetSalary,
        UnpaidNetSalary = row.NetSalary - row.PaidNetSalary,
    };

    private static PayrollReportRowDto Row<TKey>(PayrollAmountsRow<TKey> row, string label)
    {
        var dto = new PayrollReportRowDto { Label = label };
        Copy(Amounts(row), dto);
        return dto;
    }

    /// <summary>Adds up group rows; the employee count is the distinct count across all groups.</summary>
    private static PayrollAmountsDto Combine(IEnumerable<PayrollAmountsDto> rows, int employeeCount)
    {
        var total = new PayrollAmountsDto { EmployeeCount = employeeCount };
        foreach (var row in rows)
        {
            total.RecordCount += row.RecordCount;
            total.BasicSalary += row.BasicSalary;
            total.HouseRent += row.HouseRent;
            total.MedicalAllowance += row.MedicalAllowance;
            total.TransportAllowance += row.TransportAllowance;
            total.OtherAllowance += row.OtherAllowance;
            total.TotalAllowances += row.TotalAllowances;
            total.Overtime += row.Overtime;
            total.Bonus += row.Bonus;
            total.GrossSalary += row.GrossSalary;
            total.Tax += row.Tax;
            total.ProvidentFund += row.ProvidentFund;
            total.LeaveDeduction += row.LeaveDeduction;
            total.AdvanceDeduction += row.AdvanceDeduction;
            total.LoanDeduction += row.LoanDeduction;
            total.OtherDeduction += row.OtherDeduction;
            total.OtherDeductions += row.OtherDeductions;
            total.TotalDeduction += row.TotalDeduction;
            total.NetSalary += row.NetSalary;
            total.PaidNetSalary += row.PaidNetSalary;
            total.UnpaidNetSalary += row.UnpaidNetSalary;
        }

        return total;
    }

    private static void Copy(PayrollAmountsDto from, PayrollAmountsDto to)
    {
        foreach (var property in typeof(PayrollAmountsDto).GetProperties())
        {
            property.SetValue(to, property.GetValue(from));
        }
    }

    // ---- export tables ----

    private static ExportTable PeriodsTable(List<PayrollPeriodHistoryDto> periods)
    {
        var table = new ExportTable("Payroll history",
            new("Payroll period"), new("Start", ExportKind.Date), new("End", ExportKind.Date), new("Status"),
            new("Employees", ExportKind.Integer), new("Basic salary", ExportKind.Amount), new("Allowances", ExportKind.Amount),
            new("Overtime", ExportKind.Amount), new("Bonus", ExportKind.Amount), new("Gross salary", ExportKind.Amount),
            new("Tax", ExportKind.Amount), new("Other deductions", ExportKind.Amount), new("Total deduction", ExportKind.Amount),
            new("Net payroll", ExportKind.Amount), new("Processed at", ExportKind.DateTime), new("Finalized at", ExportKind.DateTime));
        foreach (var p in periods)
        {
            table.Add(p.Name, p.StartDate, p.EndDate, p.Status, p.EmployeeCount, p.BasicSalary, p.TotalAllowances, p.Overtime, p.Bonus,
                p.GrossSalary, p.Tax, p.OtherDeductions, p.TotalDeduction, p.NetSalary, p.CalculatedAt, p.FinalizedAt);
        }

        return table;
    }

    private static ExportTable SummaryTable(PayrollReportDto report)
    {
        var table = new ExportTable("Monthly payroll summary",
            new("Payroll period"), new("Status"), new("Employees", ExportKind.Integer), new("Basic salary", ExportKind.Amount),
            new("Allowances", ExportKind.Amount), new("Overtime", ExportKind.Amount), new("Bonus", ExportKind.Amount),
            new("Gross salary", ExportKind.Amount), new("Tax", ExportKind.Amount), new("Other deductions", ExportKind.Amount),
            new("Total deduction", ExportKind.Amount), new("Net salary", ExportKind.Amount), new("Paid", ExportKind.Amount), new("Outstanding", ExportKind.Amount));
        foreach (var r in report.Rows.Append(TotalRow(report)))
        {
            table.Add(r.Label, r.PeriodStatus, r.EmployeeCount, r.BasicSalary, r.TotalAllowances, r.Overtime, r.Bonus, r.GrossSalary,
                r.Tax, r.OtherDeductions, r.TotalDeduction, r.NetSalary, r.PaidNetSalary, r.UnpaidNetSalary);
        }

        return table;
    }

    private static ExportTable DepartmentsTable(PayrollReportDto report)
    {
        var table = new ExportTable("Department payroll",
            new("Department"), new("Employees", ExportKind.Integer), new("Basic salary", ExportKind.Amount), new("Allowances", ExportKind.Amount),
            new("Gross salary", ExportKind.Amount), new("Tax", ExportKind.Amount), new("Total deduction", ExportKind.Amount), new("Net salary", ExportKind.Amount));
        foreach (var r in report.Rows.Append(TotalRow(report)))
        {
            table.Add(r.Label, r.EmployeeCount, r.BasicSalary, r.TotalAllowances, r.GrossSalary, r.Tax, r.TotalDeduction, r.NetSalary);
        }

        return table;
    }

    private static ExportTable DeductionsTable(PayrollReportDto report)
    {
        var table = new ExportTable("Deduction summary",
            new(report.GroupBy == "department" ? "Department" : "Payroll period"), new("Employees", ExportKind.Integer),
            new("Tax", ExportKind.Amount), new("Provident fund", ExportKind.Amount), new("Unpaid leave", ExportKind.Amount),
            new("Advance", ExportKind.Amount), new("Loan", ExportKind.Amount), new("Other", ExportKind.Amount), new("Total deduction", ExportKind.Amount));
        foreach (var r in report.Rows.Append(TotalRow(report)))
        {
            table.Add(r.Label, r.EmployeeCount, r.Tax, r.ProvidentFund, r.LeaveDeduction, r.AdvanceDeduction, r.LoanDeduction, r.OtherDeduction, r.TotalDeduction);
        }

        return table;
    }

    private static ExportTable AllowancesTable(PayrollReportDto report)
    {
        var table = new ExportTable("Allowance and bonus summary",
            new(report.GroupBy == "department" ? "Department" : "Payroll period"), new("Employees", ExportKind.Integer),
            new("House rent", ExportKind.Amount), new("Medical", ExportKind.Amount), new("Transport", ExportKind.Amount),
            new("Other allowance", ExportKind.Amount), new("Total allowances", ExportKind.Amount), new("Overtime", ExportKind.Amount), new("Bonus", ExportKind.Amount));
        foreach (var r in report.Rows.Append(TotalRow(report)))
        {
            table.Add(r.Label, r.EmployeeCount, r.HouseRent, r.MedicalAllowance, r.TransportAllowance, r.OtherAllowance, r.TotalAllowances, r.Overtime, r.Bonus);
        }

        return table;
    }

    private async Task<ExportTable> EmployeesTableAsync(PayrollReportFilter filter, CancellationToken cancellationToken)
    {
        var (records, total) = await _repository.SearchRecordsAsync(filter, 1, MaxExportRows, cancellationToken);
        if (total > MaxExportRows)
        {
            throw new BadRequestException($"The export would contain {total} rows; narrow the filters to at most {MaxExportRows}.");
        }

        var table = new ExportTable("Employee payroll",
            new("Employee ID"), new("Employee"), new("Department"), new("Designation"), new("Payroll period"),
            new("Basic salary", ExportKind.Amount), new("Allowances", ExportKind.Amount), new("Overtime", ExportKind.Amount),
            new("Bonus", ExportKind.Amount), new("Gross salary", ExportKind.Amount), new("Tax", ExportKind.Amount),
            new("Other deductions", ExportKind.Amount), new("Total deduction", ExportKind.Amount), new("Net salary", ExportKind.Amount),
            new("Payslip"), new("Payment status"));
        foreach (var r in records.Select(r => PayrollService.ToDto(r, isHrOrAdmin: true)))
        {
            table.Add(r.EmployeeCode, r.EmployeeName, r.DepartmentName, r.DesignationName, r.PeriodName, r.BasicSalary, r.TotalAllowances,
                r.OvertimeAmount, r.Bonus, r.GrossSalary, r.Tax, r.OtherDeductions, r.TotalDeduction, r.NetSalary, r.PayslipNumber, r.PaymentStatus);
        }

        return table;
    }

    private static PayrollReportRowDto TotalRow(PayrollReportDto report)
    {
        var row = new PayrollReportRowDto { Label = "Total" };
        Copy(report.Totals, row);
        return row;
    }

    // ---- validation ----

    private static PayrollReportFilter ReportFilter(PayrollReportQueryDto query)
    {
        ValidateCalendar(query.Month, query.Year, query.From, query.To);
        var status = ParseEnum<PayrollPeriodStatus>(query.Status, "payroll status");
        return new PayrollReportFilter(
            query.Year,
            query.Month,
            query.PayrollPeriodId,
            query.EmployeeId,
            query.DepartmentId,
            query.DesignationId,
            status is null ? PayrollReportQueries.IssuedStatuses : new[] { status.Value },
            query.From,
            query.To,
            InputText.Optional(query.Search));
    }

    private static PayrollPeriodHistoryFilter PeriodFilter(PayrollPeriodHistoryQueryDto query)
    {
        ValidateCalendar(query.Month, query.Year, query.From, query.To);
        return new PayrollPeriodHistoryFilter(InputText.Optional(query.Search), query.Year, query.Month,
            ParseEnum<PayrollPeriodStatus>(query.Status, "payroll status"), query.From, query.To);
    }

    private static void ValidateCalendar(int? month, int? year, DateOnly? from, DateOnly? to)
    {
        if (month is < 1 or > 12)
        {
            throw new BadRequestException("The month must be between 1 and 12.");
        }

        if (year is < 2000 or > 2100)
        {
            throw new BadRequestException("The year must be between 2000 and 2100.");
        }

        if (from > to)
        {
            throw new BadRequestException("The 'from' date can't be after the 'to' date.");
        }
    }

    private static (int Page, int PageSize) Paging(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var size = pageSize ?? DefaultPageSize;
        if (p < 1 || size is < 1 or > MaxPageSize)
        {
            throw new BadRequestException($"The page must be 1 or more and the pageSize between 1 and {MaxPageSize}.");
        }

        return (p, size);
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string label) where TEnum : struct, Enum
    {
        var text = InputText.Optional(value);
        if (text is null)
        {
            return null;
        }

        if (!Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(text, out _))
        {
            throw new BadRequestException($"'{value}' is not a valid {label}. Allowed values: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        return parsed;
    }

    /// <summary>The filters of an export, for the audit entry (ids and calendar only).</summary>
    private static string FilterText(PayrollReportQueryDto query)
    {
        var parts = new List<string>();
        if (query.Year is not null) parts.Add($"year={query.Year}");
        if (query.Month is not null) parts.Add($"month={query.Month}");
        if (query.Status is not null) parts.Add($"status={query.Status}");
        if (query.PayrollPeriodId is not null) parts.Add($"period={query.PayrollPeriodId}");
        if (query.EmployeeId is not null) parts.Add($"employee={query.EmployeeId}");
        if (query.DepartmentId is not null) parts.Add($"department={query.DepartmentId}");
        if (query.DesignationId is not null) parts.Add($"designation={query.DesignationId}");
        if (query.From is not null || query.To is not null) parts.Add($"dates={query.From:yyyy-MM-dd}..{query.To:yyyy-MM-dd}");
        return parts.Count == 0 ? string.Empty : "; " + string.Join(", ", parts);
    }
}
