using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Validation;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Employees.Dtos;

public class CreateEmployeeDto
{
    [Required]
    [MaxLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

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

    /// <summary>Optional; defaults to FullTime when omitted.</summary>
    [EnumDataType(typeof(EmploymentType))]
    public EmploymentType? EmploymentType { get; set; }

    /// <summary>Monthly basic salary. Optional; cannot be negative.</summary>
    [Range(typeof(decimal), "0", "9999999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The BasicSalary field must be between 0 and 9999999999999999.99.")]
    public decimal? BasicSalary { get; set; }
}
