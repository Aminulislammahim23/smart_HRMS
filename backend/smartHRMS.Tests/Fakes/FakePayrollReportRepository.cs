using smartHRMS.Application.Features.PayrollReports;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Tests.Fakes;

/// <summary>
/// Runs the production <see cref="PayrollReportQueries"/> over the in-memory payroll fake, so the report filters and
/// aggregations are tested exactly as SQL Server receives them (LINQ to objects instead of EF).
/// </summary>
public class FakePayrollReportRepository : IPayrollReportRepository
{
    private readonly FakePayrollRepository _payroll;
    private readonly FakeEmployeeRepository _employees;

    public FakePayrollReportRepository(FakePayrollRepository payroll, FakeEmployeeRepository employees)
    {
        _payroll = payroll;
        _employees = employees;
    }

    private IQueryable<PayrollRecord> Records(PayrollReportFilter filter)
    {
        var records = _payroll.Records.ToList();
        foreach (var record in records)
        {
            record.Employee ??= _employees.Employees.FirstOrDefault(e => e.Id == record.EmployeeId);
        }

        return PayrollReportQueries.Filter(records.AsQueryable(), filter);
    }

    public Task<List<PayrollAmountsRow<Guid>>> SumByPeriodAsync(PayrollReportFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(WithPaid(PayrollReportQueries.Sum(Records(filter), r => r.PayrollPeriodId).ToList(),
            PayrollReportQueries.SumNet(PayrollReportQueries.Paid(Records(filter)), r => r.PayrollPeriodId).ToList()));

    public Task<List<PayrollAmountsRow<string?>>> SumByDepartmentAsync(PayrollReportFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(WithPaid(PayrollReportQueries.Sum(Records(filter), r => r.DepartmentName).ToList(),
            PayrollReportQueries.SumNet(PayrollReportQueries.Paid(Records(filter)), r => r.DepartmentName).ToList()));

    public Task<int> CountEmployeesAsync(PayrollReportFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(Records(filter).Select(r => r.EmployeeId).Distinct().Count());

    public Task<(List<PayrollRecord> Items, int TotalCount)> SearchRecordsAsync(PayrollReportFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var list = Records(filter).OrderByDescending(r => r.PayrollPeriod!.StartDate).ThenBy(r => r.EmployeeCode).ToList();
        return Task.FromResult((list.Skip((page - 1) * pageSize).Take(pageSize).ToList(), list.Count));
    }

    public Task<(List<PayrollPeriod> Items, int TotalCount)> SearchPeriodsAsync(PayrollPeriodHistoryFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var list = PayrollReportQueries.Filter(_payroll.Periods.AsQueryable(), filter).OrderByDescending(p => p.StartDate).ToList();
        return Task.FromResult((list.Skip((page - 1) * pageSize).Take(pageSize).ToList(), list.Count));
    }

    public Task<List<PayrollPeriod>> GetPeriodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult(_payroll.Periods.Where(p => ids.Contains(p.Id)).ToList());

    private static List<PayrollAmountsRow<TKey>> WithPaid<TKey>(List<PayrollAmountsRow<TKey>> rows, List<KeyValuePair<TKey, decimal>> paid)
    {
        foreach (var row in rows)
        {
            row.PaidNetSalary = paid.Where(p => Equals(p.Key, row.Key)).Sum(p => p.Value);
        }

        return rows;
    }
}
