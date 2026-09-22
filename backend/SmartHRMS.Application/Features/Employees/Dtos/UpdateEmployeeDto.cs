using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Validation;

namespace smartHRMS.Application.Features.Employees.Dtos;

public class UpdateEmployeeDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [MaxLength(30)]
    public string? Phone { get; set; }

    [NotDefault]
    public DateTime DateOfBirth { get; set; }

    [NotDefault]
    public DateTime JoiningDate { get; set; }

    [NotDefault]
    public Guid DepartmentId { get; set; }

    [NotDefault]
    public Guid DesignationId { get; set; }
}
