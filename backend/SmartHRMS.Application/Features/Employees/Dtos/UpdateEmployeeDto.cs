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

    private string? _phone;

    /// <summary>Optional. A blank value (e.g. "" from an empty form field) is treated as "no phone" rather than an invalid number.</summary>
    [Phone]
    [MaxLength(30)]
    public string? Phone
    {
        get => _phone;
        set => _phone = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [NotDefault]
    public DateTime DateOfBirth { get; set; }

    [NotDefault]
    public DateTime JoiningDate { get; set; }

    [NotDefault]
    public Guid DepartmentId { get; set; }

    [NotDefault]
    public Guid DesignationId { get; set; }
}
