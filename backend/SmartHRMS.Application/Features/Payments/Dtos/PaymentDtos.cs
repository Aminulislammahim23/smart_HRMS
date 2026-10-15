using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Models;
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

    /// <summary>Day 21: Admin, payment Paid: correct its reference or date through the audited correction workflow.</summary>
    public bool CanCorrect { get; set; }
}

public class PaymentStatusHistoryDto
{
    /// <summary>Null for the creation of the payment.</summary>
    public string? PreviousStatus { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public string? Reason { get; set; }

    /// <summary>The transfer reference of this step (paid, failed attempt or correction), when one was recorded.</summary>
    public string? Reference { get; set; }

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

    /// <summary>Department stored on the payroll record.</summary>
    public string? DepartmentName { get; set; }

    public Guid PayrollPeriodId { get; set; }

    public string PeriodName { get; set; } = string.Empty;

    public Guid? PayslipId { get; set; }

    public string? PayslipNumber { get; set; }

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

    /// <summary>Processing attempts (the first one plus retries); single-payment endpoint only.</summary>
    public int? AttemptCount { get; set; }

    /// <summary>Failed attempts, each kept in the history with its reason and reference; single-payment endpoint only.</summary>
    public int? FailedAttemptCount { get; set; }

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
    /// <summary>
    /// Transfer reference: required for BankTransfer, MobileBanking and Cheque (Day 21). For Cash and Other a reference
    /// (e.g. receipt number) or <see cref="Remarks"/> is required. No credentials, PINs or OTPs.
    /// </summary>
    [MaxLength(100)]
    [RegularExpression(PaymentReference.Pattern, ErrorMessage = PaymentReference.Message)]
    public string? TransactionReference { get; set; }

    /// <summary>Date the salary was paid; defaults to today and can't be in the future or before the period starts.</summary>
    public DateOnly? PaymentDate { get; set; }

    /// <summary>Evidence notes (e.g. "cash handed over, signed receipt"); kept in the status history.</summary>
    [MaxLength(400)]
    public string? Remarks { get; set; }
}

/// <summary>The allowed characters of a transfer reference, so no credential can be typed into it by accident.</summary>
public static class PaymentReference
{
    public const string Pattern = @"^[A-Za-z0-9 ._/#:-]*$";
    public const string Message = "The transaction reference may contain letters, digits, spaces and . _ / # : - only.";
}

public class PaymentReasonDto
{
    /// <summary>Required when marking a payment failed or cancelling it; optional for a retry.</summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}

/// <summary>Processing → Failed: reason required; the failed attempt's provider reference is optional and kept in history.</summary>
public class MarkPaymentFailedDto : PaymentReasonDto
{
    [MaxLength(100)]
    [RegularExpression(PaymentReference.Pattern, ErrorMessage = PaymentReference.Message)]
    public string? TransactionReference { get; set; }
}

/// <summary>Failed → Processing: optional reason, and optionally another payment method for the new attempt.</summary>
public class RetryPaymentDto : PaymentReasonDto
{
    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod? PaymentMethod { get; set; }
}

/// <summary>
/// Day 21: correction of a Paid payment's evidence (reference and/or payment date). The payment stays Paid and its
/// amount can't change; a reason is required and the old values stay in the status history and the audit log.
/// </summary>
public class CorrectPaymentDto
{
    [MaxLength(100)]
    [RegularExpression(PaymentReference.Pattern, ErrorMessage = PaymentReference.Message)]
    public string? TransactionReference { get; set; }

    public DateOnly? PaymentDate { get; set; }

    [MaxLength(300)]
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

    /// <summary>The employee's current department (HR/Admin lists only; ignored on the employee's own list).</summary>
    public Guid? DepartmentId { get; set; }

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

// ---- Day 21: disbursement dashboard ----

/// <summary>
/// Disbursement figures of eligible payroll (Finalized or Paid), all from the database:
/// - payable: net salary &gt; 0 with an issued payslip; <see cref="PayableAmount"/> sums those finalized net salaries;
/// - paid: the payslip is paid (by its one Paid payment); outstanding = payable − paid;
/// - pending / processing / failed: the record's active (non-cancelled) payment is in that status;
/// - not initiated: payable, unpaid and in no active payment (cancelled payments don't count).
/// </summary>
public class DisbursementFiguresDto
{
    public int PayableCount { get; set; }

    public decimal PayableAmount { get; set; }

    public int PaidCount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public int NotInitiatedCount { get; set; }

    public decimal NotInitiatedAmount { get; set; }

    public int PendingCount { get; set; }

    public decimal PendingAmount { get; set; }

    public int ProcessingCount { get; set; }

    public decimal ProcessingAmount { get; set; }

    public int FailedCount { get; set; }

    public decimal FailedAmount { get; set; }
}

public class PayrollDisbursementDto : DisbursementFiguresDto
{
    public Guid PayrollPeriodId { get; set; }

    public string PeriodName { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>Finalized or Paid.</summary>
    public string PayrollStatus { get; set; } = string.Empty;

    /// <summary>True while the payroll has a batch that is neither Paid nor Cancelled.</summary>
    public bool HasOpenBatch { get; set; }

    /// <summary>Admin, Finalized, no open batch and someone is not yet in a payment.</summary>
    public bool CanCreateBatch { get; set; }
}

public class DisbursementSummaryDto
{
    /// <summary>Totals over every eligible payroll matching the filters (not only the current page).</summary>
    public DisbursementFiguresDto Totals { get; set; } = new();

    /// <summary>One row per eligible payroll, newest period first.</summary>
    public PagedResult<PayrollDisbursementDto> Payrolls { get; set; } = new();
}

public class DisbursementSummaryQueryDto
{
    public Guid? PayrollPeriodId { get; set; }

    /// <summary>Payroll periods starting in this year.</summary>
    public int? Year { get; set; }

    public int? Page { get; set; }

    public int? PageSize { get; set; }
}
