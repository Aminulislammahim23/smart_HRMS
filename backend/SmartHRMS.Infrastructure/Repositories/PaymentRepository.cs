using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public PaymentRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentBatch?> GetBatchAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.PaymentBatches
            .AsSplitQuery()
            .Include(b => b.PayrollPeriod)
            .Include(b => b.Items).ThenInclude(i => i.Transaction!).ThenInclude(t => t.History)
            .Include(b => b.Items).ThenInclude(i => i.PayrollRecord!).ThenInclude(r => r.Payslip)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<(List<PaymentBatch> Items, int TotalCount)> SearchBatchesAsync(
        Guid? payrollPeriodId, PaymentBatchStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.PaymentBatches.AsNoTracking();
        if (payrollPeriodId is not null)
        {
            query = query.Where(b => b.PayrollPeriodId == payrollPeriodId);
        }

        if (status is not null)
        {
            query = query.Where(b => b.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .Include(b => b.PayrollPeriod)
            .Include(b => b.Items)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<bool> HasOpenBatchAsync(Guid payrollPeriodId, CancellationToken cancellationToken)
    {
        return await _dbContext.PaymentBatches.AnyAsync(b =>
            b.PayrollPeriodId == payrollPeriodId && b.Status != PaymentBatchStatus.Paid && b.Status != PaymentBatchStatus.Cancelled, cancellationToken);
    }

    public async Task<HashSet<Guid>> GetRecordIdsWithActivePaymentAsync(Guid payrollPeriodId, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.PaymentBatchItems
            .Where(i => i.PayrollRecord!.PayrollPeriodId == payrollPeriodId && i.Status != PaymentTransactionStatus.Cancelled)
            .Select(i => i.PayrollRecordId)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public async Task<int> CountBatchNumbersAsync(string prefix, CancellationToken cancellationToken)
    {
        return await _dbContext.PaymentBatches.CountAsync(b => b.BatchNumber.StartsWith(prefix), cancellationToken);
    }

    public async Task<PaymentTransaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.PaymentTransactions
            .AsSplitQuery()
            .Include(t => t.History)
            .Include(t => t.PaymentBatchItem!).ThenInclude(i => i.PayrollRecord!).ThenInclude(r => r.Payslip)
            .Include(t => t.PaymentBatchItem!).ThenInclude(i => i.PayrollRecord!).ThenInclude(r => r.PayrollPeriod)
            .Include(t => t.PaymentBatch!).ThenInclude(b => b.Items)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(List<PaymentTransaction> Items, int TotalCount)> SearchTransactionsAsync(PaymentFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.PaymentTransactions.AsNoTracking();

        if (filter.BatchId is not null)
        {
            query = query.Where(t => t.PaymentBatchId == filter.BatchId);
        }

        if (filter.EmployeeId is not null)
        {
            query = query.Where(t => t.EmployeeId == filter.EmployeeId);
        }

        if (filter.PayrollPeriodId is not null)
        {
            query = query.Where(t => t.PaymentBatch!.PayrollPeriodId == filter.PayrollPeriodId);
        }

        if (filter.Status is not null)
        {
            query = query.Where(t => t.Status == filter.Status);
        }

        if (filter.Method is not null)
        {
            query = query.Where(t => t.PaymentMethod == filter.Method);
        }

        // Paid date when paid, otherwise the batch's planned date.
        if (filter.From is not null)
        {
            query = query.Where(t => (t.PaymentDate ?? t.PaymentBatch!.PaymentDate) >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(t => (t.PaymentDate ?? t.PaymentBatch!.PaymentDate) <= filter.To);
        }

        if (filter.Search is not null)
        {
            var search = filter.Search;
            query = query.Where(t => t.PaymentBatchItem!.PayrollRecord!.EmployeeCode.Contains(search)
                || t.PaymentBatchItem.PayrollRecord.EmployeeName.Contains(search)
                || (t.TransactionReference != null && t.TransactionReference.Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.PaymentDate ?? t.PaymentBatch!.PaymentDate)
            .ThenBy(t => t.PaymentBatchItem!.PayrollRecord!.EmployeeCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .Include(t => t.PaymentBatch)
            .Include(t => t.PaymentBatchItem!).ThenInclude(i => i.PayrollRecord!).ThenInclude(r => r.PayrollPeriod)
            .Include(t => t.PaymentBatchItem!).ThenInclude(i => i.PayrollRecord!).ThenInclude(r => r.Payslip)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddBatchAsync(PaymentBatch batch, CancellationToken cancellationToken)
    {
        await _dbContext.PaymentBatches.AddAsync(batch, cancellationToken);
    }

    public void AddHistory(PaymentStatusHistory history)
    {
        _dbContext.PaymentStatusHistories.Add(history);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
