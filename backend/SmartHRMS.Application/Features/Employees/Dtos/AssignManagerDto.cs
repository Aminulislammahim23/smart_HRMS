namespace smartHRMS.Application.Features.Employees.Dtos;

/// <summary>Sets or clears (null) an employee's line manager.</summary>
public class AssignManagerDto
{
    public Guid? ManagerId { get; set; }
}
