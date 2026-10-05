using smartHRMS.Application.Common.Exceptions;
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
