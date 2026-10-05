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
            query = query.Include(p => p.Records).ThenInclude(r => r.Payslip);
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
            (p.Status == PayrollPeriodStatus.PendingApproval || p.Status == PayrollPeriodStatus.Approved
                || p.Status == PayrollPeriodStatus.Finalized || p.Status == PayrollPeriodStatus.Paid)
            && p.StartDate <= to && p.EndDate >= from, cancellationToken);
    }

    public async Task<PayrollRecord?> GetRecordAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.PayrollRecords
            .Include(r => r.PayrollPeriod)
            .Include(r => r.Payslip)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<PayrollRecord>> SearchRecordsAsync(PayrollRecordFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.PayrollRecords
            .AsNoTracking()
            .Include(r => r.PayrollPeriod)
            .Include(r => r.Payslip)
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

    public async Task<(List<PayrollRecord> Items, int TotalCount)> SearchHistoryAsync(PayrollHistoryFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var query = _dbContext.PayrollRecords.AsNoTracking();

        if (filter.EmployeeId is not null)
        {
            query = query.Where(r => r.EmployeeId == filter.EmployeeId);
        }

        // Department filter on the employee's current assignment (the record keeps only the department name).
        if (filter.DepartmentId is not null)
        {
            query = query.Where(r => r.Employee!.DepartmentId == filter.DepartmentId);
        }

        if (filter.Year is not null)
        {
            query = query.Where(r => r.PayrollPeriod!.StartDate.Year == filter.Year);
        }

        if (filter.Month is not null)
        {
            query = query.Where(r => r.PayrollPeriod!.StartDate.Month == filter.Month);
        }

        if (filter.PeriodStatus is not null)
        {
            query = query.Where(r => r.PayrollPeriod!.Status == filter.PeriodStatus);
        }

        if (filter.OnlyWithPayslip)
        {
            query = query.Where(r => r.Payslip != null);
        }

        if (filter.PaymentStatus is not null)
        {
            query = query.Where(r => r.Payslip!.PaymentStatus == filter.PaymentStatus);
        }

        if (filter.Search is not null)
        {
            var search = filter.Search;
            query = query.Where(r => r.EmployeeCode.Contains(search) || r.EmployeeName.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);

        var ordered = (page.SortBy, page.Descending) switch
        {
            (PayrollHistorySort.Employee, false) => query.OrderBy(r => r.EmployeeName).ThenByDescending(r => r.PayrollPeriod!.StartDate),
            (PayrollHistorySort.Employee, true) => query.OrderByDescending(r => r.EmployeeName).ThenByDescending(r => r.PayrollPeriod!.StartDate),
            (PayrollHistorySort.Gross, false) => query.OrderBy(r => r.GrossSalary).ThenBy(r => r.EmployeeCode),
            (PayrollHistorySort.Gross, true) => query.OrderByDescending(r => r.GrossSalary).ThenBy(r => r.EmployeeCode),
            (PayrollHistorySort.Net, false) => query.OrderBy(r => r.NetSalary).ThenBy(r => r.EmployeeCode),
            (PayrollHistorySort.Net, true) => query.OrderByDescending(r => r.NetSalary).ThenBy(r => r.EmployeeCode),
            (PayrollHistorySort.PaymentDate, false) => query.OrderBy(r => r.Payslip!.PaymentDate).ThenBy(r => r.EmployeeCode),
            (PayrollHistorySort.PaymentDate, true) => query.OrderByDescending(r => r.Payslip!.PaymentDate).ThenBy(r => r.EmployeeCode),
            (_, false) => query.OrderBy(r => r.PayrollPeriod!.StartDate).ThenBy(r => r.EmployeeCode),
            _ => query.OrderByDescending(r => r.PayrollPeriod!.StartDate).ThenBy(r => r.EmployeeCode),
        };

        var items = await ordered
            .Include(r => r.PayrollPeriod)
            .Include(r => r.Payslip)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<Payslip?> GetPayslipAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Payslips
            .Include(p => p.PayrollRecord)
                .ThenInclude(r => r!.PayrollPeriod)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Dictionary<Guid, PayslipCounts>> GetPayslipCountsAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken cancellationToken)
    {
        var ids = periodIds.ToList();
        var counts = await _dbContext.Payslips
            .AsNoTracking()
            .Where(p => ids.Contains(p.PayrollPeriodId))
            .GroupBy(p => p.PayrollPeriodId)
            .Select(g => new { PeriodId = g.Key, Issued = g.Count(), Paid = g.Count(p => p.PaymentStatus == PaymentStatus.Paid) })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.PeriodId, c => new PayslipCounts(c.Issued, c.Paid));
    }

    public async Task AddPayslipAsync(Payslip payslip, CancellationToken cancellationToken)
    {
        await _dbContext.Payslips.AddAsync(payslip, cancellationToken);
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
