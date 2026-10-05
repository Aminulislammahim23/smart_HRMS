using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Tests.Fakes;

/// <summary>
/// In-memory payment store. Saving enforces the same uniqueness rules as the database indexes (one open batch per
/// payroll, one active payment per payroll record), reporting a violation as a 409 like SmartHRMSDbContext does.
/// </summary>
public class FakePaymentRepository : IPaymentRepository
{
    private readonly List<PaymentBatch> _pending = new();

    public List<PaymentBatch> Batches { get; } = new();

    public IEnumerable<PaymentTransaction> Transactions => Batches.SelectMany(b => b.Items).Select(i => i.Transaction!);

    public int SaveCount { get; private set; }

    public Task<PaymentBatch?> GetBatchAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.FirstOrDefault(b => b.Id == id));

    public Task<(List<PaymentBatch> Items, int TotalCount)> SearchBatchesAsync(Guid? payrollPeriodId, PaymentBatchStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Batches.Where(b => (payrollPeriodId == null || b.PayrollPeriodId == payrollPeriodId) && (status == null || b.Status == status)).ToList();
        return Task.FromResult((query.Skip((page - 1) * pageSize).Take(pageSize).ToList(), query.Count));
    }

    public Task<bool> HasOpenBatchAsync(Guid payrollPeriodId, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.Any(b => b.PayrollPeriodId == payrollPeriodId && b.Status is not (PaymentBatchStatus.Paid or PaymentBatchStatus.Cancelled)));

    public Task<HashSet<Guid>> GetRecordIdsWithActivePaymentAsync(Guid payrollPeriodId, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.Where(b => b.PayrollPeriodId == payrollPeriodId)
            .SelectMany(b => b.Items)
            .Where(i => i.Status != PaymentTransactionStatus.Cancelled)
            .Select(i => i.PayrollRecordId)
            .ToHashSet());

    public Task<int> CountBatchNumbersAsync(string prefix, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.Count(b => b.BatchNumber.StartsWith(prefix, StringComparison.Ordinal)));

    public Task<PaymentTransaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Transactions.FirstOrDefault(t => t.Id == id));

    public Task<(List<PaymentTransaction> Items, int TotalCount)> SearchTransactionsAsync(PaymentFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Transactions.Where(t =>
            (filter.BatchId == null || t.PaymentBatchId == filter.BatchId)
            && (filter.EmployeeId == null || t.EmployeeId == filter.EmployeeId)
            && (filter.PayrollPeriodId == null || t.PaymentBatch!.PayrollPeriodId == filter.PayrollPeriodId)
            && (filter.Status == null || t.Status == filter.Status)
            && (filter.Method == null || t.PaymentMethod == filter.Method)
            && (filter.From == null || (t.PaymentDate ?? t.PaymentBatch!.PaymentDate) >= filter.From)
            && (filter.To == null || (t.PaymentDate ?? t.PaymentBatch!.PaymentDate) <= filter.To)
            && (filter.Search == null || t.PaymentBatchItem!.PayrollRecord!.EmployeeCode.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)
                || t.PaymentBatchItem.PayrollRecord.EmployeeName.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.PaymentBatchItem!.PayrollRecord!.EmployeeCode)
            .ToList();
        return Task.FromResult((query.Skip((page - 1) * pageSize).Take(pageSize).ToList(), query.Count));
    }

    public Task AddBatchAsync(PaymentBatch batch, CancellationToken cancellationToken)
    {
        _pending.Add(batch);
        return Task.CompletedTask;
    }

    /// <summary>Counts history rows registered explicitly, as the EF repository must insert them.</summary>
    public int HistoryAdds { get; private set; }

    public void AddHistory(PaymentStatusHistory history) => HistoryAdds++;

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var all = Batches.Concat(_pending).ToList();
        var open = all.Where(b => b.Status is not (PaymentBatchStatus.Paid or PaymentBatchStatus.Cancelled)).GroupBy(b => b.PayrollPeriodId);
        var activeItems = all.SelectMany(b => b.Items).Where(i => i.Status != PaymentTransactionStatus.Cancelled).GroupBy(i => i.PayrollRecordId);
        if (open.Any(g => g.Count() > 1) || activeItems.Any(g => g.Count() > 1))
        {
            _pending.Clear();
            throw new ConflictException("A record with the same unique value already exists.");
        }

        Batches.AddRange(_pending);
        _pending.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }
}
