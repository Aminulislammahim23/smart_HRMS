namespace smartHRMS.Domain.Enums;

/// <summary>How a salary payment is made. A controlled list; free text is not accepted.</summary>
public enum PaymentMethod
{
    BankTransfer = 1,
    Cash = 2,
    MobileBanking = 3,
    Cheque = 4,
    Other = 5
}

/// <summary>
/// One employee's salary payment. Allowed transitions: Pending → Processing, Pending → Cancelled, Processing → Paid,
/// Processing → Failed, Failed → Processing (retry). Paid and Cancelled are final.
/// </summary>
public enum PaymentTransactionStatus
{
    Pending = 1,
    Processing = 2,
    Paid = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>Derived from the batch's payments (cancelled payments are ignored unless all are cancelled).</summary>
public enum PaymentBatchStatus
{
    Pending = 1,
    Processing = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Failed = 5,
    Cancelled = 6
}
