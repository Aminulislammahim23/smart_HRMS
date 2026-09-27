using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Validation;
using smartHRMS.Domain.Enums;

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

    /// <summary>Like Phone, this is a full update: omitting Address or Gender clears the stored value.</summary>
    [MaxLength(500)]
    public string? Address { get; set; }

    [EnumDataType(typeof(Gender))]
    public Gender? Gender { get; set; }

    [NotDefault]
    public DateTime DateOfBirth { get; set; }

    [NotDefault]
    public DateTime JoiningDate { get; set; }

    [NotDefault]
    public Guid DepartmentId { get; set; }

    [NotDefault]
    public Guid DesignationId { get; set; }

    /// <summary>Optional; omitting it keeps the current employment type (so older clients keep working).</summary>
    [EnumDataType(typeof(EmploymentType))]
    public EmploymentType? EmploymentType { get; set; }

    /// <summary>
    /// Optional; omitting it keeps the current status. Setting Active/OnLeave on a former employee reactivates them,
    /// which requires their department and designation to be active.
    /// </summary>
    [EnumDataType(typeof(EmployeeStatus))]
    public EmployeeStatus? Status { get; set; }
}
