using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Features.PayrollReports;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

/// <summary>Runs the shared <see cref="PayrollReportQueries"/> in SQL Server: grouping and sums happen in the database.</summary>
public class PayrollReportRepository : IPayrollReportRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public PayrollReportRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private IQueryable<PayrollRecord> Records(PayrollReportFilter filter) =>
        PayrollReportQueries.Filter(_dbContext.PayrollRecords.AsNoTracking(), filter);

    public async Task<List<PayrollAmountsRow<Guid>>> SumByPeriodAsync(PayrollReportFilter filter, CancellationToken cancellationToken)
    {
        var rows = await PayrollReportQueries.Sum(Records(filter), r => r.PayrollPeriodId).ToListAsync(cancellationToken);
        var paid = await PayrollReportQueries.SumNet(PayrollReportQueries.Paid(Records(filter)), r => r.PayrollPeriodId).ToListAsync(cancellationToken);
        var paidByKey = paid.ToDictionary(p => p.Key, p => p.Value);
        foreach (var row in rows)
        {
            row.PaidNetSalary = paidByKey.GetValueOrDefault(row.Key);
        }

        return rows;
    }

    public async Task<List<PayrollAmountsRow<string?>>> SumByDepartmentAsync(PayrollReportFilter filter, CancellationToken cancellationToken)
    {
        var rows = await PayrollReportQueries.Sum(Records(filter), r => r.DepartmentName).ToListAsync(cancellationToken);
        var paid = await PayrollReportQueries.SumNet(PayrollReportQueries.Paid(Records(filter)), r => r.DepartmentName).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.PaidNetSalary = paid.Where(p => p.Key == row.Key).Sum(p => p.Value);
        }

        return rows;
    }

    public async Task<int> CountEmployeesAsync(PayrollReportFilter filter, CancellationToken cancellationToken)
    {
        return await Records(filter).Select(r => r.EmployeeId).Distinct().CountAsync(cancellationToken);
    }

    public async Task<(List<PayrollRecord> Items, int TotalCount)> SearchRecordsAsync(PayrollReportFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Records(filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.PayrollPeriod!.StartDate)
            .ThenBy(r => r.EmployeeCode)
            .Include(r => r.PayrollPeriod)
            .Include(r => r.Payslip)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(List<PayrollPeriod> Items, int TotalCount)> SearchPeriodsAsync(PayrollPeriodHistoryFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = PayrollReportQueries.Filter(_dbContext.PayrollPeriods.AsNoTracking(), filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<List<PayrollPeriod>> GetPeriodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.ToList();
        return await _dbContext.PayrollPeriods.AsNoTracking().Where(p => list.Contains(p.Id)).ToListAsync(cancellationToken);
    }
}
