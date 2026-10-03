using System.ComponentModel.DataAnnotations;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Leaves.Dtos;

public class LeaveRequestDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Annual, Sick, Casual or Unpaid.</summary>
    public string LeaveType { get; set; } = string.Empty;

    public bool IsPaid { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>Working days in the range (weekends excluded).</summary>
    public int TotalDays { get; set; }

    public string? Reason { get; set; }

    /// <summary>Pending, Approved, Rejected or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public string? RequestedBy { get; set; }

    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }

    /// <summary>Whether the signed-in user may approve or reject this request (computed by the server).</summary>
    public bool CanReview { get; set; }

    /// <summary>Whether the signed-in user may cancel this request (computed by the server).</summary>
    public bool CanCancel { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class LeaveTypeDto
{
    public string Name { get; set; } = string.Empty;

    public bool IsPaid { get; set; }
}

public class CreateLeaveRequestDto
{
    /// <summary>
    /// Optional. Employees and Managers always apply for themselves (a different id is refused); HR and Admin may
    /// apply on behalf of any current employee.
    /// </summary>
    public Guid? EmployeeId { get; set; }

    [Required]
    [EnumDataType(typeof(LeaveType))]
    public LeaveType? LeaveType { get; set; }

    [Required]
    public DateOnly? StartDate { get; set; }

    [Required]
    public DateOnly? EndDate { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class ReviewLeaveRequestDto
{
    [MaxLength(500)]
    public string? Comment { get; set; }
}

/// <summary>Query-string filters; all optional.</summary>
public class LeaveQueryDto
{
    /// <summary>mine (own requests), team (direct reports, Managers), or all (HR/Admin). Default: all you may see.</summary>
    public string? Scope { get; set; }

    public Guid? EmployeeId { get; set; }

    public string? Status { get; set; }

    public string? LeaveType { get; set; }

    /// <summary>Requests overlapping StartDate..EndDate.</summary>
    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }
}
