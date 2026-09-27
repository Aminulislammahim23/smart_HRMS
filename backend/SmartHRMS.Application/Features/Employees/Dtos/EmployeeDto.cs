namespace smartHRMS.Application.Features.Employees.Dtos;

public class EmployeeDto
{
    public Guid Id { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>"FirstName LastName". Computed for display; not stored.</summary>
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateTime DateOfBirth { get; set; }

    public DateTime JoiningDate { get; set; }

    public Guid DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public Guid DesignationId { get; set; }

    public string? DesignationName { get; set; }

    /// <summary>FullTime, PartTime, Contract or Intern.</summary>
    public string EmploymentType { get; set; } = string.Empty;

    public decimal? BasicSalary { get; set; }

    /// <summary>Employment status: Active, Inactive, Resigned, Terminated or OnLeave.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>True while the employee still works here (Status is Active or OnLeave). Derived from Status, not stored.</summary>
    public bool IsActive { get; set; }

    /// <summary>Relative URL of the employee's profile photo, or null if none has been uploaded.</summary>
    public string? PhotoUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
