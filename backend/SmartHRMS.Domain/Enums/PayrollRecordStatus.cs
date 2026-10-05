namespace smartHRMS.Domain.Enums;

/// <summary>
/// Calculated: ready for review. NeedsReview: the calculation needs HR attention (e.g. a negative net salary) and
/// blocks submission. The others follow the payroll period.
/// </summary>
public enum PayrollRecordStatus
{
    Calculated = 1,
    NeedsReview = 2,
    Approved = 3,
    Paid = 4,
    Cancelled = 5,
    Finalized = 6
}
