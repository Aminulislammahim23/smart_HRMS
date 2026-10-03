using smartHRMS.Application.Features.Payroll;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Interfaces;

public interface IPayrollRepository
{
    /// <summary>Tracked. With <paramref name="includeRecords"/>, the period's records and their payslips are loaded (tracked) too.</summary>
    Task<PayrollPeriod?> GetPeriodAsync(Guid id, bool includeRecords, CancellationToken cancellationToken);

    /// <summary>Read-only; newest start date first.</summary>
    Task<List<PayrollPeriod>> SearchPeriodsAsync(int? year, PayrollPeriodStatus? status, CancellationToken cancellationToken);

    /// <summary>Employee count and money totals per period id (periods without records are absent).</summary>
    Task<Dictionary<Guid, PayrollTotals>> GetTotalsAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken cancellationToken);

    /// <summary>True when a period that is not Cancelled (other than <paramref name="excludeId"/>) overlaps the range.</summary>
    Task<bool> HasOverlappingPeriodAsync(DateOnly from, DateOnly to, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>
    /// True when a period overlapping the range is PendingApproval, Approved or Paid: its numbers are under review or
    /// final, so leave in that range must not change any more.
    /// </summary>
    Task<bool> IsDateRangeLockedAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Tracked, with the period and payslip loaded.</summary>
    Task<PayrollRecord?> GetRecordAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only page of records with period and payslip loaded, plus the total matching count.</summary>
    Task<(List<PayrollRecord> Items, int TotalCount)> SearchHistoryAsync(PayrollHistoryFilter filter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked, with its record and the record's period loaded.</summary>
    Task<Payslip?> GetPayslipAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Issued and paid payslips per period id (periods without payslips are absent).</summary>
    Task<Dictionary<Guid, PayslipCounts>> GetPayslipCountsAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken cancellationToken);

    Task AddPayslipAsync(Payslip payslip, CancellationToken cancellationToken);

    /// <summary>Read-only, with periods loaded; by period (newest first), then employee code.</summary>
    Task<List<PayrollRecord>> SearchRecordsAsync(PayrollRecordFilter filter, CancellationToken cancellationToken);

    Task AddPeriodAsync(PayrollPeriod period, CancellationToken cancellationToken);

    void RemovePeriod(PayrollPeriod period);

    Task AddRecordAsync(PayrollRecord record, CancellationToken cancellationToken);

    void RemoveRecord(PayrollRecord record);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
