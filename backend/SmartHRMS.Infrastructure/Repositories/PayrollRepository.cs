using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class PayrollRepository : IPayrollRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public PayrollRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PayrollPeriod?> GetPeriodAsync(Guid id, bool includeRecords, CancellationToken cancellationToken)
    {
        var query = _dbContext.PayrollPeriods.AsQueryable();
        if (includeRecords)
        {
            query = query.Include(p => p.Records);
        }

        return await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<PayrollPeriod>> SearchPeriodsAsync(int? year, PayrollPeriodStatus? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.PayrollPeriods.AsNoTracking();

        if (year is not null)
        {
            var first = new DateOnly(year.Value, 1, 1);
            var last = new DateOnly(year.Value, 12, 31);
            query = query.Where(p => p.StartDate <= last && p.EndDate >= first);
        }

        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        return await query.OrderByDescending(p => p.StartDate).ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, PayrollTotals>> GetTotalsAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken cancellationToken)
    {
        var ids = periodIds.ToList();
        var totals = await _dbContext.PayrollRecords
            .AsNoTracking()
            .Where(r => ids.Contains(r.PayrollPeriodId))
            .GroupBy(r => r.PayrollPeriodId)
            .Select(g => new
            {
                PeriodId = g.Key,
                Count = g.Count(),
                Gross = g.Sum(r => r.GrossSalary),
                Deduction = g.Sum(r => r.TotalDeduction),
                Net = g.Sum(r => r.NetSalary),
                NeedsReview = g.Count(r => r.Status == PayrollRecordStatus.NeedsReview),
            })
            .ToListAsync(cancellationToken);

        return totals.ToDictionary(t => t.PeriodId, t => new PayrollTotals(t.Count, t.Gross, t.Deduction, t.Net, t.NeedsReview));
    }

    public async Task<bool> HasOverlappingPeriodAsync(DateOnly from, DateOnly to, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.PayrollPeriods.AnyAsync(p =>
            p.Status != PayrollPeriodStatus.Cancelled
            && p.StartDate <= to && p.EndDate >= from
            && (excludeId == null || p.Id != excludeId), cancellationToken);
    }

    public async Task<bool> IsDateRangeLockedAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        return await _dbContext.PayrollPeriods.AnyAsync(p =>
            (p.Status == PayrollPeriodStatus.PendingApproval || p.Status == PayrollPeriodStatus.Approved || p.Status == PayrollPeriodStatus.Paid)
            && p.StartDate <= to && p.EndDate >= from, cancellationToken);
    }

    public async Task<PayrollRecord?> GetRecordAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.PayrollRecords
            .Include(r => r.PayrollPeriod)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<PayrollRecord>> SearchRecordsAsync(PayrollRecordFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.PayrollRecords
            .AsNoTracking()
            .Include(r => r.PayrollPeriod)
            .AsQueryable();

        if (filter.PeriodId is not null)
        {
            query = query.Where(r => r.PayrollPeriodId == filter.PeriodId);
        }

        if (filter.EmployeeId is not null)
        {
            query = query.Where(r => r.EmployeeId == filter.EmployeeId);
        }

        // Department/designation filter on the employee's current assignment.
        if (filter.DepartmentId is not null)
        {
            query = query.Where(r => r.Employee!.DepartmentId == filter.DepartmentId);
        }

        if (filter.DesignationId is not null)
        {
            query = query.Where(r => r.Employee!.DesignationId == filter.DesignationId);
        }

        if (filter.Status is not null)
        {
            query = query.Where(r => r.Status == filter.Status);
        }

        if (filter.Search is not null)
        {
            var search = filter.Search;
            query = query.Where(r => r.EmployeeCode.Contains(search) || r.EmployeeName.Contains(search));
        }

        if (filter.PeriodStatuses is not null)
        {
            var statuses = filter.PeriodStatuses.ToList();
            query = query.Where(r => statuses.Contains(r.PayrollPeriod!.Status));
        }

        return await query
            .OrderByDescending(r => r.PayrollPeriod!.StartDate)
            .ThenBy(r => r.EmployeeCode)
            .ToListAsync(cancellationToken);
    }

    public async Task AddPeriodAsync(PayrollPeriod period, CancellationToken cancellationToken)
    {
        await _dbContext.PayrollPeriods.AddAsync(period, cancellationToken);
    }

    public void RemovePeriod(PayrollPeriod period)
    {
        _dbContext.PayrollPeriods.Remove(period);
    }

    public async Task AddRecordAsync(PayrollRecord record, CancellationToken cancellationToken)
    {
        await _dbContext.PayrollRecords.AddAsync(record, cancellationToken);
    }

    public void RemoveRecord(PayrollRecord record)
    {
        _dbContext.PayrollRecords.Remove(record);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
