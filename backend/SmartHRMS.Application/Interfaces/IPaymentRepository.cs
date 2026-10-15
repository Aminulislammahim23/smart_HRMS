using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Interfaces;

/// <summary>Already-validated payment list criteria; null means "no restriction".</summary>
public sealed record PaymentFilter(
    Guid? BatchId,
    Guid? EmployeeId,
    Guid? PayrollPeriodId,
    PaymentTransactionStatus? Status,
    PaymentMethod? Method,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    Guid? DepartmentId = null);

/// <summary>Active (non-cancelled) payments of one payroll in one status, summed in the database (Day 21).</summary>
public sealed record ActivePaymentTotal(Guid PayrollPeriodId, PaymentTransactionStatus Status, int Count, decimal Amount);

public interface IPaymentRepository
{
    /// <summary>Tracked, with its items, their transactions (and history) and payroll records loaded.</summary>
    Task<PaymentBatch?> GetBatchAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only page with payroll periods and items loaded; newest first.</summary>
    Task<(List<PaymentBatch> Items, int TotalCount)> SearchBatchesAsync(Guid? payrollPeriodId, PaymentBatchStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>True when the payroll has a batch that is neither Paid nor Cancelled.</summary>
    Task<bool> HasOpenBatchAsync(Guid payrollPeriodId, CancellationToken cancellationToken);

    /// <summary>Payroll record ids already in a batch item that isn't Cancelled (paid or being paid).</summary>
    Task<HashSet<Guid>> GetRecordIdsWithActivePaymentAsync(Guid payrollPeriodId, CancellationToken cancellationToken);

    /// <summary>Batch numbers starting with <paramref name="prefix"/> (for the next sequence number).</summary>
    Task<int> CountBatchNumbersAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>Tracked, with history, batch item (+ payroll record and payslip) and batch (+ items) loaded.</summary>
    Task<PaymentTransaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only page with batch, item and payroll record (+ period, payslip) loaded; newest first.</summary>
    Task<(List<PaymentTransaction> Items, int TotalCount)> SearchTransactionsAsync(PaymentFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Count and amount of active (non-cancelled) payment items per payroll and status (Day 21 dashboard).</summary>
    Task<List<ActivePaymentTotal>> GetActivePaymentTotalsAsync(IReadOnlyCollection<Guid> payrollPeriodIds, CancellationToken cancellationToken);

    /// <summary>The payrolls among <paramref name="payrollPeriodIds"/> that have a batch that is neither Paid nor Cancelled.</summary>
    Task<HashSet<Guid>> GetPeriodIdsWithOpenBatchAsync(IReadOnlyCollection<Guid> payrollPeriodIds, CancellationToken cancellationToken);

    Task AddBatchAsync(PaymentBatch batch, CancellationToken cancellationToken);

    /// <summary>Tracks a new status-history row of an existing payment so it is inserted on save.</summary>
    void AddHistory(PaymentStatusHistory history);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
