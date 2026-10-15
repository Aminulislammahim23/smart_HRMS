using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Payments;

/// <summary>The payment state machine and the derived batch status, in one place.</summary>
public static class PaymentRules
{
    private static readonly Dictionary<PaymentTransactionStatus, PaymentTransactionStatus[]> Allowed = new()
    {
        [PaymentTransactionStatus.Pending] = [PaymentTransactionStatus.Processing, PaymentTransactionStatus.Cancelled],
        [PaymentTransactionStatus.Processing] = [PaymentTransactionStatus.Paid, PaymentTransactionStatus.Failed],
        [PaymentTransactionStatus.Failed] = [PaymentTransactionStatus.Processing],
        [PaymentTransactionStatus.Paid] = [],
        [PaymentTransactionStatus.Cancelled] = [],
    };

    public static bool CanMove(PaymentTransactionStatus from, PaymentTransactionStatus to) => Allowed[from].Contains(to);

    /// <summary>Electronic and cheque payments always leave a reference (transfer ID, mobile transaction ID, cheque number).</summary>
    public static bool RequiresReference(PaymentMethod method) =>
        method is PaymentMethod.BankTransfer or PaymentMethod.MobileBanking or PaymentMethod.Cheque;

    /// <summary>
    /// Day 21: a payment is confirmed only with evidence. Bank transfer, mobile banking and cheque need the transaction
    /// reference; cash and other need a reference (e.g. a receipt number) or remarks describing the evidence.
    /// </summary>
    public static void EnsureEvidence(PaymentMethod method, string? reference, string? remarks)
    {
        if (RequiresReference(method) && reference is null)
        {
            throw new BadRequestException($"A transaction reference is required to confirm a {method} payment.");
        }

        if (reference is null && remarks is null)
        {
            throw new BadRequestException("Payment evidence is required: enter a reference (e.g. receipt number) or remarks.");
        }
    }

    /// <summary>
    /// Financial integrity before money is recorded as paid: the amount must still be the finalized net salary of the
    /// locked payroll record (no partial payments, no client amounts). A mismatch is a 409, never silently "fixed".
    /// </summary>
    public static void EnsureAmountMatchesPayroll(PaymentTransaction transaction, PaymentBatchItem item, PayrollRecord record, PayrollPeriod period)
    {
        if (!period.IsLocked || period.Status is not (PayrollPeriodStatus.Finalized or PayrollPeriodStatus.Paid))
        {
            throw new ConflictException("Payroll must be finalized before payment.");
        }

        if (transaction.Amount != record.NetSalary || item.Amount != record.NetSalary)
        {
            throw new ConflictException("The payment amount no longer matches the finalized net salary; it can't be confirmed.");
        }
    }

    /// <summary>409 with a clear message unless <paramref name="from"/> → <paramref name="to"/> is allowed.</summary>
    public static void EnsureCanMove(PaymentTransactionStatus from, PaymentTransactionStatus to)
    {
        if (CanMove(from, to))
        {
            return;
        }

        throw new ConflictException(from switch
        {
            PaymentTransactionStatus.Paid => "Payment has already been completed.",
            PaymentTransactionStatus.Cancelled => "Payment has been cancelled.",
            _ => $"Invalid payment status transition: {from} → {to}.",
        });
    }

    /// <summary>
    /// Batch status from its payments. Cancelled payments are ignored unless every payment is cancelled. Then: any
    /// Processing → Processing; all Paid → Paid; some Paid → PartiallyPaid; any Failed → Failed; otherwise Pending.
    /// </summary>
    public static PaymentBatchStatus BatchStatus(IReadOnlyCollection<PaymentTransactionStatus> statuses)
    {
        var active = statuses.Where(s => s != PaymentTransactionStatus.Cancelled).ToList();
        if (active.Count == 0)
        {
            return PaymentBatchStatus.Cancelled;
        }

        if (active.Contains(PaymentTransactionStatus.Processing))
        {
            return PaymentBatchStatus.Processing;
        }

        if (active.All(s => s == PaymentTransactionStatus.Paid))
        {
            return PaymentBatchStatus.Paid;
        }

        if (active.Contains(PaymentTransactionStatus.Paid))
        {
            return PaymentBatchStatus.PartiallyPaid;
        }

        return active.Contains(PaymentTransactionStatus.Failed) ? PaymentBatchStatus.Failed : PaymentBatchStatus.Pending;
    }
}
