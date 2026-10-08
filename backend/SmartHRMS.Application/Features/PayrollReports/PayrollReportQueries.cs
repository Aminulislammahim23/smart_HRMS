using System.Linq.Expressions;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.PayrollReports;

/// <summary>Validated report criteria; null means "no restriction". <see cref="Statuses"/> null means any status.</summary>
public sealed record PayrollReportFilter(
    int? Year,
    int? Month,
    Guid? PayrollPeriodId,
    Guid? EmployeeId,
    Guid? DepartmentId,
    Guid? DesignationId,
    IReadOnlyCollection<PayrollPeriodStatus>? Statuses,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    IReadOnlyCollection<Guid>? PeriodIds = null);

/// <summary>Validated payroll period history criteria.</summary>
public sealed record PayrollPeriodHistoryFilter(string? Search, int? Year, int? Month, PayrollPeriodStatus? Status, DateOnly? From, DateOnly? To);

/// <summary>Summed amounts of one group of payroll records (key: period id, department name, or 0 for everything).</summary>
public sealed class PayrollAmountsRow<TKey>
{
    public TKey Key { get; set; } = default!;

    public int EmployeeCount { get; set; }

    public int RecordCount { get; set; }

    public decimal BasicSalary { get; set; }

    public decimal HouseRent { get; set; }

    public decimal MedicalAllowance { get; set; }

    public decimal TransportAllowance { get; set; }

    public decimal OtherAllowance { get; set; }

    public decimal Overtime { get; set; }

    public decimal Bonus { get; set; }

    public decimal GrossSalary { get; set; }

    public decimal Tax { get; set; }

    public decimal ProvidentFund { get; set; }

    public decimal LeaveDeduction { get; set; }

    public decimal AdvanceDeduction { get; set; }

    public decimal LoanDeduction { get; set; }

    public decimal OtherDeduction { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal NetSalary { get; set; }

    /// <summary>Filled from a second query (net salary of paid payslips).</summary>
    public decimal PaidNetSalary { get; set; }
}

/// <summary>
/// The report filters and aggregations, written once as LINQ so that SQL Server (through EF Core) and the in-memory
/// test fakes run exactly the same logic. Nothing is recalculated: amounts are the stored payroll record values.
/// </summary>
public static class PayrollReportQueries
{
    /// <summary>Default report scope: issued payroll only.</summary>
    public static readonly IReadOnlyCollection<PayrollPeriodStatus> IssuedStatuses =
        new[] { PayrollPeriodStatus.Approved, PayrollPeriodStatus.Finalized, PayrollPeriodStatus.Paid };

    public static IQueryable<PayrollRecord> Filter(IQueryable<PayrollRecord> query, PayrollReportFilter filter)
    {
        if (filter.PeriodIds is not null)
        {
            var ids = filter.PeriodIds.ToList();
            query = query.Where(r => ids.Contains(r.PayrollPeriodId));
        }

        if (filter.PayrollPeriodId is not null)
        {
            query = query.Where(r => r.PayrollPeriodId == filter.PayrollPeriodId);
        }

        if (filter.EmployeeId is not null)
        {
            query = query.Where(r => r.EmployeeId == filter.EmployeeId);
        }

        // Department/designation: the employee's current assignment, as in the Day 17 payroll history.
        if (filter.DepartmentId is not null)
        {
            query = query.Where(r => r.Employee!.DepartmentId == filter.DepartmentId);
        }

        if (filter.DesignationId is not null)
        {
            query = query.Where(r => r.Employee!.DesignationId == filter.DesignationId);
        }

        if (filter.Statuses is not null)
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(r => statuses.Contains(r.PayrollPeriod!.Status));
        }

        if (filter.Year is not null)
        {
            query = query.Where(r => r.PayrollPeriod!.StartDate.Year == filter.Year);
        }

        if (filter.Month is not null)
        {
            query = query.Where(r => r.PayrollPeriod!.StartDate.Month == filter.Month);
        }

        if (filter.From is not null)
        {
            query = query.Where(r => r.PayrollPeriod!.EndDate >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(r => r.PayrollPeriod!.StartDate <= filter.To);
        }

        if (filter.Search is not null)
        {
            var search = filter.Search;
            query = query.Where(r => r.EmployeeCode.Contains(search) || r.EmployeeName.Contains(search));
        }

        return query;
    }

    public static IQueryable<PayrollPeriod> Filter(IQueryable<PayrollPeriod> query, PayrollPeriodHistoryFilter filter)
    {
        if (filter.Search is not null)
        {
            var search = filter.Search;
            query = query.Where(p => p.Name.Contains(search));
        }

        if (filter.Year is not null)
        {
            query = query.Where(p => p.StartDate.Year == filter.Year);
        }

        if (filter.Month is not null)
        {
            query = query.Where(p => p.StartDate.Month == filter.Month);
        }

        if (filter.Status is not null)
        {
            query = query.Where(p => p.Status == filter.Status);
        }

        if (filter.From is not null)
        {
            query = query.Where(p => p.EndDate >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(p => p.StartDate <= filter.To);
        }

        return query;
    }

    /// <summary>Records whose payslip is paid (for <see cref="PayrollAmountsRow{TKey}.PaidNetSalary"/>).</summary>
    public static IQueryable<PayrollRecord> Paid(IQueryable<PayrollRecord> query) =>
        query.Where(r => r.Payslip != null && r.Payslip.PaymentStatus == PaymentStatus.Paid);

    public static IQueryable<PayrollAmountsRow<TKey>> Sum<TKey>(IQueryable<PayrollRecord> query, Expression<Func<PayrollRecord, TKey>> key) =>
        query.GroupBy(key).Select(g => new PayrollAmountsRow<TKey>
        {
            Key = g.Key,
            EmployeeCount = g.Select(r => r.EmployeeId).Distinct().Count(),
            RecordCount = g.Count(),
            BasicSalary = g.Sum(r => r.BasicSalary),
            HouseRent = g.Sum(r => r.HouseRent),
            MedicalAllowance = g.Sum(r => r.MedicalAllowance),
            TransportAllowance = g.Sum(r => r.TransportAllowance),
            OtherAllowance = g.Sum(r => r.OtherAllowance),
            Overtime = g.Sum(r => r.OvertimeAmount),
            Bonus = g.Sum(r => r.Bonus),
            GrossSalary = g.Sum(r => r.GrossSalary),
            Tax = g.Sum(r => r.Tax),
            ProvidentFund = g.Sum(r => r.ProvidentFund),
            LeaveDeduction = g.Sum(r => r.LeaveDeduction),
            AdvanceDeduction = g.Sum(r => r.AdvanceDeduction),
            LoanDeduction = g.Sum(r => r.LoanDeduction),
            OtherDeduction = g.Sum(r => r.OtherDeduction),
            TotalDeduction = g.Sum(r => r.TotalDeduction),
            NetSalary = g.Sum(r => r.NetSalary),
        });

    public static IQueryable<KeyValuePair<TKey, decimal>> SumNet<TKey>(IQueryable<PayrollRecord> query, Expression<Func<PayrollRecord, TKey>> key) =>
        query.GroupBy(key).Select(g => new KeyValuePair<TKey, decimal>(g.Key, g.Sum(r => r.NetSalary)));
}
