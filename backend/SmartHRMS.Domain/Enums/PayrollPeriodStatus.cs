namespace smartHRMS.Domain.Enums;

/// <summary>
/// Draft → Calculated → PendingApproval → Approved → Finalized → Paid. Draft, Calculated and PendingApproval can be
/// Cancelled. Approved, Finalized and Paid payroll is locked; payments can only be made for Finalized payroll.
/// </summary>
public enum PayrollPeriodStatus
{
    Draft = 1,
    Calculated = 2,
    PendingApproval = 3,
    Approved = 4,
    Paid = 5,
    Cancelled = 6,
    Finalized = 7
}
