namespace smartHRMS.Domain.Enums;

/// <summary>Only Approved leave counts for attendance and payroll; Pending, Rejected and Cancelled never do.</summary>
public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}
