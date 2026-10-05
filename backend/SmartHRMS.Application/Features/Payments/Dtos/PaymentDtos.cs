using System.ComponentModel.DataAnnotations;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Payments.Dtos;

public class PaymentCountsDto
{
    public int Pending { get; set; }

    public int Processing { get; set; }

    public int Paid { get; set; }

    public int Failed { get; set; }

    public int Cancelled { get; set; }
}

public class PaymentBatchActionsDto
{
    /// <summary>Admin, batch has Pending payments: start processing them.</summary>
    public bool CanProcess { get; set; }

    /// <summary>Admin, every payment still Pending: cancel the whole batch.</summary>
    public bool CanCancel { get; set; }
}

public class PaymentBatchDto
{
    public Guid Id { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public Guid PayrollPeriodId { get; set; }

    public string PeriodName { get; set; } = string.Empty;

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public DateOnly PaymentDate { get; set; }

    /// <summary>BankTransfer, Cash, MobileBanking, Cheque or Other.</summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Calculated by the server from the batch items (cancelled items excluded).</summary>
    public int TotalEmployees { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    /// <summary>Pending, Processing, PartiallyPaid, Paid, Failed or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public PaymentCountsDto Counts { get; set; } = new();

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public PaymentBatchActionsDto Actions { get; set; } = new();
}

public class PaymentActionsDto
{
    public bool CanMarkPaid { get; set; }

    public bool CanMarkFailed { get; set; }

    public bool CanRetry { get; set; }

    public bool CanCancel { get; set; }
}

public class PaymentStatusHistoryDto
{
    /// <summary>Null for the creation of the payment.</summary>
    public string? PreviousStatus { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }
}

/// <summary>One employee's salary payment (a payment transaction).</summary>
public class PaymentDto
{
    public Guid Id { get; set; }

    public Guid PaymentBatchId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public Guid PaymentBatchItemId { get; set; }

    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    public Guid PayrollPeriodId { get; set; }

    public string PeriodName { get; set; } = string.Empty;

    public Guid? PayslipId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Pending, Processing, Paid, Failed or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public string? TransactionReference { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>The office date the salary was paid, once Paid; otherwise the batch's planned date.</summary>
    public DateOnly? PaymentDate { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Status changes, oldest first (single-payment endpoint only).</summary>
    public List<PaymentStatusHistoryDto>? History { get; set; }

    public PaymentActionsDto Actions { get; set; } = new();
}

public class CreatePaymentBatchDto
{
    [Required]
    public Guid? PayrollPeriodId { get; set; }

    [Required]
    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Planned payment date; defaults to today (office date) and can't be before the payroll period starts.</summary>
    public DateOnly? PaymentDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class MarkPaymentPaidDto
{
    /// <summary>Bank/mobile transfer reference (optional). No credentials, PINs or OTPs.</summary>
    [MaxLength(100)]
    [RegularExpression(@"^[A-Za-z0-9 ._/#:-]*$", ErrorMessage = "The transaction reference may contain letters, digits, spaces and . _ / # : - only.")]
    public string? TransactionReference { get; set; }

    /// <summary>Date the salary was paid; defaults to today and can't be in the future or before the period starts.</summary>
    public DateOnly? PaymentDate { get; set; }
}

public class PaymentReasonDto
{
    /// <summary>Required when marking a payment failed or cancelling it; optional for a retry.</summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class PaymentBatchQueryDto
{
    public Guid? PayrollPeriodId { get; set; }

    public string? Status { get; set; }

    public int? Page { get; set; }

    public int? PageSize { get; set; }
}

public class PaymentQueryDto
{
    public Guid? BatchId { get; set; }

    /// <summary>HR/Admin lists only; ignored on the employee's own list.</summary>
    public Guid? EmployeeId { get; set; }

    public Guid? PayrollPeriodId { get; set; }

    public string? Status { get; set; }

    public string? PaymentMethod { get; set; }

    /// <summary>Payment date range (paid date, or the batch's planned date while unpaid).</summary>
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    /// <summary>Employee code/name or transaction reference contains.</summary>
    [MaxLength(100)]
    public string? Search { get; set; }

    public int? Page { get; set; }

    public int? PageSize { get; set; }
}
