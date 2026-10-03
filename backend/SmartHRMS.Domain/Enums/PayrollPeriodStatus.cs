namespace smartHRMS.Domain.Enums;

/// <summary>
/// Draft → Calculated → PendingApproval → Approved → Paid. Draft, Calculated and PendingApproval can be Cancelled.
/// Approved and Paid payroll is locked.
/// </summary>
public enum PayrollPeriodStatus
{
    Draft = 1,
    Calculated = 2,
    PendingApproval = 3,
    Approved = 4,
    Paid = 5,
    Cancelled = 6
}
