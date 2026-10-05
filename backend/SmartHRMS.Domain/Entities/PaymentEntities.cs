using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// The salary payments of one finalized payroll period, made together. Totals are calculated by the server from the
/// batch items. A payroll has at most one open batch (not Paid or Cancelled) at a time.
/// </summary>
public class PaymentBatch : BaseEntity
{
    /// <summary>"PAY-{period start yyyyMM}-{sequence:0000}", unique.</summary>
    public string BatchNumber { get; set; } = string.Empty;

    public Guid PayrollPeriodId { get; set; }

    public PayrollPeriod? PayrollPeriod { get; set; }

    /// <summary>Planned payment date (office date).</summary>
    public DateOnly PaymentDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public int TotalEmployees { get; set; }

    public decimal TotalAmount { get; set; }

    public PaymentBatchStatus Status { get; set; } = PaymentBatchStatus.Pending;

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PaymentBatchItem> Items { get; set; } = new List<PaymentBatchItem>();
}

/// <summary>
/// What one employee is owed in a batch: the net salary of their (locked) payroll record. A payroll record can be in
/// at most one batch item that isn't Cancelled, so nobody can be paid twice for the same payroll.
/// </summary>
public class PaymentBatchItem : BaseEntity
{
    public Guid PaymentBatchId { get; set; }

    public PaymentBatch? PaymentBatch { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid PayrollRecordId { get; set; }

    public PayrollRecord? PayrollRecord { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Mirrors the transaction's status.</summary>
    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

    public string? PaymentReference { get; set; }

    public string? FailureReason { get; set; }

    public DateTime? PaidAt { get; set; }

    public PaymentTransaction? Transaction { get; set; }
}

/// <summary>
/// The payment of one batch item: method, status, provider reference and outcome. No bank credentials, PINs or OTPs
/// are ever stored. Every status change writes a <see cref="PaymentStatusHistory"/> row.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    public Guid PaymentBatchId { get; set; }

    public PaymentBatch? PaymentBatch { get; set; }

    public Guid PaymentBatchItemId { get; set; }

    public PaymentBatchItem? PaymentBatchItem { get; set; }

    public Guid EmployeeId { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

    /// <summary>Bank/mobile transfer reference, recorded when the payment is confirmed.</summary>
    public string? TransactionReference { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>When the payment was confirmed paid.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Office date the salary was paid.</summary>
    public DateOnly? PaymentDate { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PaymentStatusHistory> History { get; set; } = new List<PaymentStatusHistory>();
}

/// <summary>An append-only record of one payment status change (CreatedAt = when it changed).</summary>
public class PaymentStatusHistory : BaseEntity
{
    public Guid PaymentTransactionId { get; set; }

    /// <summary>Null for the creation of the payment.</summary>
    public PaymentTransactionStatus? PreviousStatus { get; set; }

    public PaymentTransactionStatus NewStatus { get; set; }

    public string? Reason { get; set; }

    public Guid? ChangedByUserId { get; set; }
}
