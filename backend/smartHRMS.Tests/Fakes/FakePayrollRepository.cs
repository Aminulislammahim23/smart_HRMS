using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Tests.Fakes;

/// <summary>
/// In-memory payroll store. Added records join their period only when saved, and the unique
/// (period, employee) rule is enforced on save, like the database index.
/// </summary>
public class FakePayrollRepository : IPayrollRepository
{
    private readonly FakeEmployeeRepository _employees;
    private readonly List<PayrollRecord> _pendingRecords = new();
    private readonly List<PayrollRecord> _removedRecords = new();

    public FakePayrollRepository(FakeEmployeeRepository employees)
    {
        _employees = employees;
    }

    public List<PayrollPeriod> Periods { get; } = new();

    public IEnumerable<PayrollRecord> Records => Periods.SelectMany(p => p.Records);

    public int SaveCount { get; private set; }

    public Task<PayrollPeriod?> GetPeriodAsync(Guid id, bool includeRecords, CancellationToken cancellationToken) =>
        Task.FromResult(Periods.FirstOrDefault(p => p.Id == id));

    public Task<List<PayrollPeriod>> SearchPeriodsAsync(int? year, PayrollPeriodStatus? status, CancellationToken cancellationToken)
    {
        var query = Periods.AsEnumerable();
        if (year is not null)
        {
            query = query.Where(p => p.StartDate.Year <= year && p.EndDate.Year >= year);
        }

        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        return Task.FromResult(query.OrderByDescending(p => p.StartDate).ToList());
    }

    public Task<Dictionary<Guid, PayrollTotals>> GetTotalsAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken cancellationToken)
    {
        return Task.FromResult(Periods
            .Where(p => periodIds.Contains(p.Id) && p.Records.Count > 0)
            .ToDictionary(
                p => p.Id,
                p => new PayrollTotals(
                    p.Records.Count,
                    p.Records.Sum(r => r.GrossSalary),
                    p.Records.Sum(r => r.TotalDeduction),
                    p.Records.Sum(r => r.NetSalary),
                    p.Records.Count(r => r.Status == PayrollRecordStatus.NeedsReview))));
    }

    public Task<bool> HasOverlappingPeriodAsync(DateOnly from, DateOnly to, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(Periods.Any(p => p.Status != PayrollPeriodStatus.Cancelled && p.StartDate <= to && p.EndDate >= from && p.Id != excludeId));

    public Task<bool> IsDateRangeLockedAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult(Periods.Any(p =>
            p.Status is PayrollPeriodStatus.PendingApproval or PayrollPeriodStatus.Approved or PayrollPeriodStatus.Paid
            && p.StartDate <= to && p.EndDate >= from));

    public Task<PayrollRecord?> GetRecordAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Records.FirstOrDefault(r => r.Id == id));

    public Task<List<PayrollRecord>> SearchRecordsAsync(PayrollRecordFilter filter, CancellationToken cancellationToken)
    {
        var query = Records.AsEnumerable();
        if (filter.PeriodId is not null)
        {
            query = query.Where(r => r.PayrollPeriodId == filter.PeriodId);
        }

        if (filter.EmployeeId is not null)
        {
            query = query.Where(r => r.EmployeeId == filter.EmployeeId);
        }

        if (filter.DepartmentId is not null)
        {
            query = query.Where(r => _employees.Employees.Any(e => e.Id == r.EmployeeId && e.DepartmentId == filter.DepartmentId));
        }

        if (filter.DesignationId is not null)
        {
            query = query.Where(r => _employees.Employees.Any(e => e.Id == r.EmployeeId && e.DesignationId == filter.DesignationId));
        }

        if (filter.Status is not null)
        {
            query = query.Where(r => r.Status == filter.Status);
        }

        if (filter.Search is not null)
        {
            query = query.Where(r => r.EmployeeCode.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)
                || r.EmployeeName.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.PeriodStatuses is not null)
        {
            query = query.Where(r => filter.PeriodStatuses.Contains(r.PayrollPeriod!.Status));
        }

        return Task.FromResult(query.OrderByDescending(r => r.PayrollPeriod!.StartDate).ThenBy(r => r.EmployeeCode).ToList());
    }

    public IEnumerable<Payslip> Payslips => Records.Where(r => r.Payslip is not null).Select(r => r.Payslip!);

    public Task<(List<PayrollRecord> Items, int TotalCount)> SearchHistoryAsync(PayrollHistoryFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var query = Records.AsEnumerable();
        if (filter.EmployeeId is not null) query = query.Where(r => r.EmployeeId == filter.EmployeeId);
        if (filter.DepartmentId is not null) query = query.Where(r => _employees.Employees.Any(e => e.Id == r.EmployeeId && e.DepartmentId == filter.DepartmentId));
        if (filter.Year is not null) query = query.Where(r => r.PayrollPeriod!.StartDate.Year == filter.Year);
        if (filter.Month is not null) query = query.Where(r => r.PayrollPeriod!.StartDate.Month == filter.Month);
        if (filter.PeriodStatus is not null) query = query.Where(r => r.PayrollPeriod!.Status == filter.PeriodStatus);
        if (filter.OnlyWithPayslip) query = query.Where(r => r.Payslip is not null);
        if (filter.PaymentStatus is not null) query = query.Where(r => r.Payslip?.PaymentStatus == filter.PaymentStatus);
        if (filter.Search is not null) query = query.Where(r => r.EmployeeCode.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) || r.EmployeeName.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));

        var list = query.ToList();
        IEnumerable<PayrollRecord> ordered = page.SortBy switch
        {
            PayrollHistorySort.Employee => list.OrderBy(r => r.EmployeeName),
            PayrollHistorySort.Gross => list.OrderBy(r => r.GrossSalary),
            PayrollHistorySort.Net => list.OrderBy(r => r.NetSalary),
            PayrollHistorySort.PaymentDate => list.OrderBy(r => r.Payslip?.PaymentDate),
            _ => list.OrderBy(r => r.PayrollPeriod!.StartDate),
        };
        if (page.Descending) ordered = ordered.Reverse();

        return Task.FromResult((ordered.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToList(), list.Count));
    }

    public Task<Payslip?> GetPayslipAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Payslips.FirstOrDefault(p => p.Id == id));

    public Task<Dictionary<Guid, PayslipCounts>> GetPayslipCountsAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken cancellationToken) =>
        Task.FromResult(Payslips
            .Where(p => periodIds.Contains(p.PayrollPeriodId))
            .GroupBy(p => p.PayrollPeriodId)
            .ToDictionary(g => g.Key, g => new PayslipCounts(g.Count(), g.Count(p => p.PaymentStatus == PaymentStatus.Paid))));

    public List<Payslip> AddedPayslips { get; } = new();

    public Task AddPayslipAsync(Payslip payslip, CancellationToken cancellationToken)
    {
        // The issuer already attached it to its record (as EF does through the navigation).
        AddedPayslips.Add(payslip);
        return Task.CompletedTask;
    }

    public Task AddPeriodAsync(PayrollPeriod period, CancellationToken cancellationToken)
    {
        Periods.Add(period);
        return Task.CompletedTask;
    }

    public void RemovePeriod(PayrollPeriod period) => Periods.Remove(period);

    public Task AddRecordAsync(PayrollRecord record, CancellationToken cancellationToken)
    {
        _pendingRecords.Add(record);
        return Task.CompletedTask;
    }

    public void RemoveRecord(PayrollRecord record) => _removedRecords.Add(record);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var removed in _removedRecords)
        {
            Periods.FirstOrDefault(p => p.Id == removed.PayrollPeriodId)?.Records.Remove(removed);
        }

        foreach (var record in _pendingRecords)
        {
            var period = Periods.First(p => p.Id == record.PayrollPeriodId);
            if (period.Records.Any(r => r.EmployeeId == record.EmployeeId))
            {
                throw new InvalidOperationException("Duplicate payroll record for the same employee and period.");
            }

            record.PayrollPeriod = period;
            period.Records.Add(record);
        }

        _pendingRecords.Clear();
        _removedRecords.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }
}
