using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// A leave application for whole days. <see cref="TotalDays"/> counts only working days (weekends excluded) and is
/// calculated by the server.
/// </summary>
public class LeaveRequest : BaseEntity
{
    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public LeaveType LeaveType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int TotalDays { get; set; }

    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    /// <summary>The user who applied (the employee, or HR applying on their behalf).</summary>
    public Guid? RequestedByUserId { get; set; }

    /// <summary>The user who approved, rejected or cancelled the request.</summary>
    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }
}
