using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Payments.Dtos;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Payments;

public interface IPaymentService
{
    Task<PaymentBatchDto> CreateBatchAsync(CreatePaymentBatchDto dto, CancellationToken cancellationToken);

    Task<PagedResult<PaymentBatchDto>> GetBatchesAsync(PaymentBatchQueryDto query, CancellationToken cancellationToken);

    Task<PaymentBatchDto> GetBatchAsync(Guid id, CancellationToken cancellationToken);

    Task<PaymentBatchDto> ProcessBatchAsync(Guid id, CancellationToken cancellationToken);

    Task<PaymentBatchDto> CancelBatchAsync(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken);

    Task<PagedResult<PaymentDto>> GetPaymentsAsync(PaymentQueryDto query, CancellationToken cancellationToken);

    Task<PagedResult<PaymentDto>> GetMyPaymentsAsync(PaymentQueryDto query, CancellationToken cancellationToken);

    Task<PaymentDto> GetPaymentAsync(Guid id, CancellationToken cancellationToken);

    Task<PaymentDto> MarkPaidAsync(Guid id, MarkPaymentPaidDto dto, CancellationToken cancellationToken);

    Task<PaymentDto> MarkFailedAsync(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken);

    Task<PaymentDto> RetryAsync(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken);

    Task<PaymentDto> CancelAsync(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken);
}

/// <summary>
/// Salary payment for Finalized payroll (Day 18). Rules:
/// - a batch is created only from Finalized, locked payroll; a payroll has at most one open batch (not Paid or
///   Cancelled); totals are calculated here from the items, never taken from the client;
/// - each payable payroll record (net salary &gt; 0, payslip not paid) gets one item + transaction; a record already in
///   an active (non-cancelled) item is never added again (also enforced by a filtered unique index);
/// - transitions follow <see cref="PaymentRules"/>; every change writes a status-history row, refreshes the batch
///   status, writes an audit entry and is saved in one transaction (row versions reject concurrent changes, 409);
/// - a paid payment is copied onto the payslip; when every payable payslip is paid, the payroll becomes Paid;
/// - viewing: HR and Admin; changing: Admin only, and never one's own salary payment;
/// - employees see only their own payments (the employee comes from the token).
/// </summary>
public class PaymentService : IPaymentService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly IPaymentRepository _paymentRepository;
    private readonly IPayrollRepository _payrollRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly AttendanceClock _clock;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IPayrollRepository payrollRepository,
        IUserRepository userRepository,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        AttendanceClock clock)
    {
        _paymentRepository = paymentRepository;
        _payrollRepository = payrollRepository;
        _userRepository = userRepository;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    // ---- batches ----

    public async Task<PaymentBatchDto> CreateBatchAsync(CreatePaymentBatchDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var periodId = dto.PayrollPeriodId!.Value;
        var period = await _payrollRepository.GetPeriodAsync(periodId, includeRecords: true, cancellationToken)
            ?? throw new NotFoundException($"Payroll period with id '{periodId}' was not found.");

        if (period.Status != PayrollPeriodStatus.Finalized || !period.IsLocked)
        {
            throw new BadRequestException(period.Status == PayrollPeriodStatus.Paid
                ? "This payroll is already fully paid."
                : "Payroll must be finalized before payment.");
        }

        if (await _paymentRepository.HasOpenBatchAsync(period.Id, cancellationToken))
        {
            throw new ConflictException("Payment batch already exists for this payroll.");
        }

        var paymentDate = dto.PaymentDate ?? _clock.Today;
        if (paymentDate < period.StartDate)
        {
            throw new BadRequestException($"The payment date can't be before the payroll period starts ({period.StartDate:yyyy-MM-dd}).");
        }

        // Duplicate protection: nobody already paid, and nobody already in an active payment, is added again.
        var alreadyInPayment = await _paymentRepository.GetRecordIdsWithActivePaymentAsync(period.Id, cancellationToken);
        var payable = period.Records
            .Where(r => PayslipIssuer.IsPayable(r) && r.Payslip is not null && r.Payslip.PaymentStatus != PaymentStatus.Paid && !alreadyInPayment.Contains(r.Id))
            .OrderBy(r => r.EmployeeCode)
            .ToList();
        if (payable.Count == 0)
        {
            throw new BadRequestException("There is nothing left to pay for this payroll.");
        }

        var method = dto.PaymentMethod!.Value;
        var userId = _currentUser.RequireUserId();
        var prefix = $"PAY-{period.StartDate:yyyyMM}-";
        var sequence = await _paymentRepository.CountBatchNumbersAsync(prefix, cancellationToken) + 1;

        var batch = new PaymentBatch
        {
            BatchNumber = $"{prefix}{sequence:0000}",
            PayrollPeriodId = period.Id,
            PayrollPeriod = period,
            PaymentDate = paymentDate,
            PaymentMethod = method,
            Status = PaymentBatchStatus.Pending,
            Notes = InputText.Optional(dto.Notes),
            CreatedByUserId = userId,
        };

        foreach (var record in payable)
        {
            var item = new PaymentBatchItem
            {
                PaymentBatchId = batch.Id,
                PaymentBatch = batch,
                EmployeeId = record.EmployeeId,
                PayrollRecordId = record.Id,
                PayrollRecord = record,
                Amount = record.NetSalary,
                Status = PaymentTransactionStatus.Pending,
            };
            var transaction = new PaymentTransaction
            {
                PaymentBatchId = batch.Id,
                PaymentBatch = batch,
                PaymentBatchItemId = item.Id,
                PaymentBatchItem = item,
                EmployeeId = record.EmployeeId,
                Amount = record.NetSalary,
                PaymentMethod = method,
                Status = PaymentTransactionStatus.Pending,
            };
            transaction.History.Add(new PaymentStatusHistory
            {
                PaymentTransactionId = transaction.Id,
                PreviousStatus = null,
                NewStatus = PaymentTransactionStatus.Pending,
                Reason = $"Created in batch {batch.BatchNumber}.",
                ChangedByUserId = userId,
            });
            item.Transaction = transaction;
            batch.Items.Add(item);
        }

        // Totals come from the items the server built, never from the request.
        batch.TotalEmployees = batch.Items.Count;
        batch.TotalAmount = batch.Items.Sum(i => i.Amount);

        await _paymentRepository.AddBatchAsync(batch, cancellationToken);
        _auditLogger.Add(AuditActions.PaymentBatchCreated, nameof(PaymentBatch), batch.Id,
            $"{batch.BatchNumber}: {batch.TotalEmployees} employees, {method}, payroll {period.Name}."); // no amounts in the audit log
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        return await MapBatchAsync(batch, cancellationToken);
    }

    public async Task<PagedResult<PaymentBatchDto>> GetBatchesAsync(PaymentBatchQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        var (page, pageSize) = Paging(query.Page, query.PageSize);
        var status = ParseEnum<PaymentBatchStatus>(query.Status, "batch status");

        var (items, total) = await _paymentRepository.SearchBatchesAsync(query.PayrollPeriodId, status, page, pageSize, cancellationToken);
        var names = await NamesAsync(items.Select(b => b.CreatedByUserId), cancellationToken);
        return new PagedResult<PaymentBatchDto> { Items = items.Select(b => MapBatch(b, names)).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<PaymentBatchDto> GetBatchAsync(Guid id, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        return await MapBatchAsync(await GetBatchEntityAsync(id, cancellationToken), cancellationToken);
    }

    public async Task<PaymentBatchDto> ProcessBatchAsync(Guid id, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var batch = await GetBatchEntityAsync(id, cancellationToken);
        var pending = batch.Items.Select(i => i.Transaction!).Where(t => t.Status == PaymentTransactionStatus.Pending).ToList();
        if (pending.Count == 0)
        {
            throw new ConflictException($"Batch {batch.BatchNumber} has no pending payments to process.");
        }

        foreach (var transaction in pending)
        {
            Move(transaction, PaymentTransactionStatus.Processing, "Batch processing started.");
        }

        RefreshBatch(batch);
        _auditLogger.Add(AuditActions.PaymentProcessingStarted, nameof(PaymentBatch), batch.Id, $"{batch.BatchNumber}: {pending.Count} payments Pending → Processing.");
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        return await MapBatchAsync(batch, cancellationToken);
    }

    public async Task<PaymentBatchDto> CancelBatchAsync(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();
        var reason = RequireReason(dto, "cancelling a batch");

        var batch = await GetBatchEntityAsync(id, cancellationToken);
        var active = batch.Items.Select(i => i.Transaction!).Where(t => t.Status != PaymentTransactionStatus.Cancelled).ToList();
        if (active.Count == 0)
        {
            throw new ConflictException($"Batch {batch.BatchNumber} is already cancelled.");
        }

        if (active.Any(t => t.Status != PaymentTransactionStatus.Pending))
        {
            throw new ConflictException($"Batch {batch.BatchNumber} can't be cancelled: some payments are already processing, paid or failed.");
        }

        foreach (var transaction in active)
        {
            Move(transaction, PaymentTransactionStatus.Cancelled, reason);
        }

        RefreshBatch(batch);
        _auditLogger.Add(AuditActions.PaymentBatchCancelled, nameof(PaymentBatch), batch.Id, $"{batch.BatchNumber}: {reason}");
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        return await MapBatchAsync(batch, cancellationToken);
    }

    // ---- payments ----

    public async Task<PagedResult<PaymentDto>> GetPaymentsAsync(PaymentQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();
        return await SearchAsync(query, query.EmployeeId, cancellationToken);
    }

    public Task<PagedResult<PaymentDto>> GetMyPaymentsAsync(PaymentQueryDto query, CancellationToken cancellationToken)
    {
        return SearchAsync(query, _currentUser.RequireEmployeeId(), cancellationToken);
    }

    public async Task<PaymentDto> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _paymentRepository.GetTransactionAsync(id, cancellationToken);
        if (transaction is null || (!_currentUser.IsHrOrAdmin() && !_currentUser.IsSelf(transaction.EmployeeId)))
        {
            // Not 403: another employee's payment id must reveal nothing.
            throw new NotFoundException($"Payment with id '{id}' was not found.");
        }

        return await MapPaymentAsync(transaction, includeHistory: true, cancellationToken);
    }

    public async Task<PaymentDto> MarkPaidAsync(Guid id, MarkPaymentPaidDto dto, CancellationToken cancellationToken)
    {
        var transaction = await GetChangeableAsync(id, cancellationToken);
        PaymentRules.EnsureCanMove(transaction.Status, PaymentTransactionStatus.Paid);

        var record = transaction.PaymentBatchItem!.PayrollRecord!;
        var period = await _payrollRepository.GetPeriodAsync(record.PayrollPeriodId, includeRecords: true, cancellationToken)
            ?? throw new NotFoundException("The payroll of this payment was not found.");
        var paymentDate = PayslipIssuer.PaymentDate(dto.PaymentDate, period, _clock.Today);
        var reference = InputText.Optional(dto.TransactionReference);
        var now = DateTime.UtcNow;
        var userId = _currentUser.RequireUserId();

        Move(transaction, PaymentTransactionStatus.Paid, reference is null ? "Payment confirmed." : $"Payment confirmed, reference {reference}.");
        transaction.TransactionReference = reference;
        transaction.FailureReason = null;
        transaction.ProcessedAt = now;
        transaction.PaymentDate = paymentDate;
        var item = transaction.PaymentBatchItem!;
        item.PaymentReference = reference;
        item.FailureReason = null;
        item.PaidAt = now;

        // The employee-facing outcome lives on the payslip.
        var payslip = record.Payslip ?? throw new ConflictException("This payroll record has no payslip.");
        PayslipIssuer.MarkPaid(payslip, paymentDate, transaction.PaymentMethod, reference, userId, now);
        var periodPaid = PayslipIssuer.CompletePeriodIfAllPaid(period, userId, now);

        RefreshBatch(transaction.PaymentBatch!);
        _auditLogger.Add(AuditActions.PaymentMarkedPaid, nameof(PaymentTransaction), transaction.Id,
            $"{record.EmployeeCode}: Processing → Paid on {paymentDate:yyyy-MM-dd}{(reference is null ? string.Empty : $", ref {reference}")}.");
        if (periodPaid)
        {
            _auditLogger.Add(AuditActions.PayrollPaid, nameof(PayrollPeriod), period.Id, "Every payable payslip paid.");
        }

        await _paymentRepository.SaveChangesAsync(cancellationToken);
        return await MapPaymentAsync(transaction, includeHistory: true, cancellationToken);
    }

    public async Task<PaymentDto> MarkFailedAsync(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken)
    {
        var reason = RequireReason(dto, "marking a payment failed");
        var transaction = await GetChangeableAsync(id, cancellationToken);
        PaymentRules.EnsureCanMove(transaction.Status, PaymentTransactionStatus.Failed);

        Move(transaction, PaymentTransactionStatus.Failed, reason);
        transaction.FailureReason = reason;
        transaction.PaymentBatchItem!.FailureReason = reason;

        RefreshBatch(transaction.PaymentBatch!);
        _auditLogger.Add(AuditActions.PaymentMarkedFailed, nameof(PaymentTransaction), transaction.Id, $"{transaction.PaymentBatchItem.PayrollRecord!.EmployeeCode}: Processing → Failed: {reason}");
        await _paymentRepository.SaveChangesAsync(cancellationToken);
        return await MapPaymentAsync(transaction, includeHistory: true, cancellationToken);
    }

    public async Task<PaymentDto> RetryAsync(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken)
    {
        var transaction = await GetChangeableAsync(id, cancellationToken);
        PaymentRules.EnsureCanMove(transaction.Status, PaymentTransactionStatus.Processing);
        if (transaction.Status != PaymentTransactionStatus.Failed)
        {
            throw new ConflictException("Only a failed payment can be retried.");
        }

        Move(transaction, PaymentTransactionStatus.Processing, InputText.Optional(dto.Reason) ?? "Retry after failure.");
        transaction.FailureReason = null;
        transaction.PaymentBatchItem!.FailureReason = null;

        RefreshBatch(transaction.PaymentBatch!);
        _auditLogger.Add(AuditActions.PaymentRetried, nameof(PaymentTransaction), transaction.Id, $"{transaction.PaymentBatchItem.PayrollRecord!.EmployeeCode}: Failed → Processing.");
        await _paymentRepository.SaveChangesAsync(cancellationToken);
        return await MapPaymentAsync(transaction, includeHistory: true, cancellationToken);
    }

    public async Task<PaymentDto> CancelAsync(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken)
    {
        var reason = RequireReason(dto, "cancelling a payment");
        var transaction = await GetChangeableAsync(id, cancellationToken);
        PaymentRules.EnsureCanMove(transaction.Status, PaymentTransactionStatus.Cancelled);

        Move(transaction, PaymentTransactionStatus.Cancelled, reason);

        RefreshBatch(transaction.PaymentBatch!);
        _auditLogger.Add(AuditActions.PaymentCancelled, nameof(PaymentTransaction), transaction.Id, $"{transaction.PaymentBatchItem!.PayrollRecord!.EmployeeCode}: Pending → Cancelled: {reason}");
        await _paymentRepository.SaveChangesAsync(cancellationToken);
        return await MapPaymentAsync(transaction, includeHistory: true, cancellationToken);
    }

    // ---- helpers ----

    /// <summary>Admin only, existing payment (404), and never the caller's own salary payment (403).</summary>
    private async Task<PaymentTransaction> GetChangeableAsync(Guid id, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();
        var transaction = await _paymentRepository.GetTransactionAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Payment with id '{id}' was not found.");

        if (_currentUser.IsSelf(transaction.EmployeeId))
        {
            throw new ForbiddenException("You can't change the status of your own salary payment; another Admin must do it.");
        }

        return transaction;
    }

    private async Task<PaymentBatch> GetBatchEntityAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _paymentRepository.GetBatchAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Payment batch with id '{id}' was not found.");
    }

    /// <summary>Validated status change of a transaction and its item, with a history row.</summary>
    private void Move(PaymentTransaction transaction, PaymentTransactionStatus to, string? reason)
    {
        PaymentRules.EnsureCanMove(transaction.Status, to);
        var now = DateTime.UtcNow;
        var history = new PaymentStatusHistory
        {
            PaymentTransactionId = transaction.Id,
            PreviousStatus = transaction.Status,
            NewStatus = to,
            Reason = reason,
            ChangedByUserId = _currentUser.RequireUserId(),
        };
        // Registered explicitly: a new row with a preset id found only through the navigation would be saved as an UPDATE.
        _paymentRepository.AddHistory(history);
        transaction.History.Add(history);
        transaction.Status = to;
        transaction.UpdatedAt = now;
        if (transaction.PaymentBatchItem is { } item)
        {
            item.Status = to;
            item.UpdatedAt = now;
        }
    }

    /// <summary>Recomputes status and totals from the items (cancelled items are left out of the totals).</summary>
    private static void RefreshBatch(PaymentBatch batch)
    {
        var active = batch.Items.Where(i => i.Status != PaymentTransactionStatus.Cancelled).ToList();
        batch.Status = PaymentRules.BatchStatus(batch.Items.Select(i => i.Status).ToList());
        batch.TotalEmployees = active.Count;
        batch.TotalAmount = active.Sum(i => i.Amount);
        batch.UpdatedAt = DateTime.UtcNow;
    }

    private static string RequireReason(PaymentReasonDto dto, string action)
    {
        return InputText.Optional(dto.Reason) ?? throw new BadRequestException($"A reason is required when {action}.");
    }

    private async Task<PagedResult<PaymentDto>> SearchAsync(PaymentQueryDto query, Guid? employeeId, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging(query.Page, query.PageSize);
        if (query.From > query.To)
        {
            throw new BadRequestException("The 'from' date can't be after the 'to' date.");
        }

        var filter = new PaymentFilter(
            query.BatchId,
            employeeId,
            query.PayrollPeriodId,
            ParseEnum<PaymentTransactionStatus>(query.Status, "payment status"),
            ParseEnum<PaymentMethod>(query.PaymentMethod, "payment method"),
            query.From,
            query.To,
            InputText.Optional(query.Search));

        var (items, total) = await _paymentRepository.SearchTransactionsAsync(filter, page, pageSize, cancellationToken);
        var result = new List<PaymentDto>(items.Count);
        foreach (var transaction in items)
        {
            result.Add(await MapPaymentAsync(transaction, includeHistory: false, cancellationToken));
        }

        return new PagedResult<PaymentDto> { Items = result, Page = page, PageSize = pageSize, TotalCount = total };
    }

    private static (int Page, int PageSize) Paging(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var size = pageSize ?? DefaultPageSize;
        if (p < 1 || size is < 1 or > MaxPageSize)
        {
            throw new BadRequestException($"The page must be 1 or more and the pageSize between 1 and {MaxPageSize}.");
        }

        return (p, size);
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string label) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out _) || !Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new BadRequestException($"'{value}' is not a valid {label}. Allowed values: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        return parsed;
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.OfType<Guid>().Distinct().ToList();
        return distinct.Count == 0 ? new Dictionary<Guid, string>() : await _userRepository.GetDisplayNamesAsync(distinct, cancellationToken);
    }

    private async Task<PaymentBatchDto> MapBatchAsync(PaymentBatch batch, CancellationToken cancellationToken)
    {
        return MapBatch(batch, await NamesAsync(new[] { batch.CreatedByUserId }, cancellationToken));
    }

    private PaymentBatchDto MapBatch(PaymentBatch batch, IReadOnlyDictionary<Guid, string> names)
    {
        int Count(PaymentTransactionStatus status) => batch.Items.Count(i => i.Status == status);
        var admin = _currentUser.IsAdmin();
        var active = batch.Items.Where(i => i.Status != PaymentTransactionStatus.Cancelled).ToList();

        return new PaymentBatchDto
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            PayrollPeriodId = batch.PayrollPeriodId,
            PeriodName = batch.PayrollPeriod?.Name ?? string.Empty,
            PeriodStartDate = batch.PayrollPeriod?.StartDate ?? default,
            PeriodEndDate = batch.PayrollPeriod?.EndDate ?? default,
            PaymentDate = batch.PaymentDate,
            PaymentMethod = batch.PaymentMethod.ToString(),
            TotalEmployees = batch.TotalEmployees,
            TotalAmount = batch.TotalAmount,
            PaidAmount = batch.Items.Where(i => i.Status == PaymentTransactionStatus.Paid).Sum(i => i.Amount),
            Status = batch.Status.ToString(),
            Notes = batch.Notes,
            Counts = new PaymentCountsDto
            {
                Pending = Count(PaymentTransactionStatus.Pending),
                Processing = Count(PaymentTransactionStatus.Processing),
                Paid = Count(PaymentTransactionStatus.Paid),
                Failed = Count(PaymentTransactionStatus.Failed),
                Cancelled = Count(PaymentTransactionStatus.Cancelled),
            },
            CreatedBy = batch.CreatedByUserId is { } id && names.TryGetValue(id, out var name) ? name : null,
            CreatedAt = batch.CreatedAt,
            UpdatedAt = batch.UpdatedAt,
            Actions = new PaymentBatchActionsDto
            {
                CanProcess = admin && Count(PaymentTransactionStatus.Pending) > 0,
                CanCancel = admin && active.Count > 0 && active.All(i => i.Status == PaymentTransactionStatus.Pending),
            },
        };
    }

    private async Task<PaymentDto> MapPaymentAsync(PaymentTransaction transaction, bool includeHistory, CancellationToken cancellationToken)
    {
        var record = transaction.PaymentBatchItem?.PayrollRecord;
        var admin = _currentUser.IsAdmin() && !_currentUser.IsSelf(transaction.EmployeeId);
        var dto = new PaymentDto
        {
            Id = transaction.Id,
            PaymentBatchId = transaction.PaymentBatchId,
            BatchNumber = transaction.PaymentBatch?.BatchNumber ?? string.Empty,
            PaymentBatchItemId = transaction.PaymentBatchItemId,
            EmployeeId = transaction.EmployeeId,
            EmployeeCode = record?.EmployeeCode ?? string.Empty,
            EmployeeName = record?.EmployeeName ?? string.Empty,
            PayrollPeriodId = record?.PayrollPeriodId ?? Guid.Empty,
            PeriodName = record?.PayrollPeriod?.Name ?? string.Empty,
            PayslipId = record?.Payslip?.Id,
            Amount = transaction.Amount,
            PaymentMethod = transaction.PaymentMethod.ToString(),
            Status = transaction.Status.ToString(),
            TransactionReference = transaction.TransactionReference,
            FailureReason = transaction.FailureReason,
            PaymentDate = transaction.PaymentDate ?? transaction.PaymentBatch?.PaymentDate,
            ProcessedAt = transaction.ProcessedAt,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
            Actions = new PaymentActionsDto
            {
                CanMarkPaid = admin && PaymentRules.CanMove(transaction.Status, PaymentTransactionStatus.Paid),
                CanMarkFailed = admin && PaymentRules.CanMove(transaction.Status, PaymentTransactionStatus.Failed),
                CanRetry = admin && transaction.Status == PaymentTransactionStatus.Failed,
                CanCancel = admin && PaymentRules.CanMove(transaction.Status, PaymentTransactionStatus.Cancelled),
            },
        };

        if (includeHistory)
        {
            var history = transaction.History.OrderBy(h => h.CreatedAt).ToList();
            var names = await NamesAsync(history.Select(h => h.ChangedByUserId), cancellationToken);
            dto.History = history.Select(h => new PaymentStatusHistoryDto
            {
                PreviousStatus = h.PreviousStatus?.ToString(),
                NewStatus = h.NewStatus.ToString(),
                Reason = h.Reason,
                ChangedBy = h.ChangedByUserId is { } id && names.TryGetValue(id, out var name) ? name : null,
                ChangedAt = h.CreatedAt,
            }).ToList();
        }

        return dto;
    }
}
