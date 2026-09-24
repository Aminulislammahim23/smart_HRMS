using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

public class Employee : BaseEntity
{
    public string EmployeeCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateTime DateOfBirth { get; set; }

    public DateTime JoiningDate { get; set; }

    public Guid DepartmentId { get; set; }

    public Department? Department { get; set; }

    public Guid DesignationId { get; set; }

    public Designation? Designation { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public string? PhotoUrl { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }
}
