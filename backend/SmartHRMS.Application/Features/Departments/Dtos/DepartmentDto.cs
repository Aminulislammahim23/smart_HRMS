namespace smartHRMS.Application.Features.Departments.Dtos;

public class DepartmentDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>All employees assigned to the department, whatever their status.</summary>
    public int EmployeeCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
