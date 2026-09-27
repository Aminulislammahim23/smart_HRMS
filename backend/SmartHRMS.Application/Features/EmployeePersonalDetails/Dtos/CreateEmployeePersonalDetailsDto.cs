using System.ComponentModel.DataAnnotations;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.EmployeePersonalDetails.Dtos;

public class CreateEmployeePersonalDetailsDto
{
    [EnumDataType(typeof(Gender))]
    public Gender? Gender { get; set; }

    [EnumDataType(typeof(MaritalStatus))]
    public MaritalStatus? MaritalStatus { get; set; }

    [EnumDataType(typeof(BloodGroup))]
    public BloodGroup? BloodGroup { get; set; }

    [MaxLength(100)]
    public string? Nationality { get; set; }

    /// <summary>Bangladesh NID: 10, 13 or 17 digits. Optional; must be unique across employees.</summary>
    [RegularExpression(@"^\s*(\d{10}|\d{13}|\d{17})\s*$", ErrorMessage = "The NationalId must be 10, 13 or 17 digits.")]
    public string? NationalId { get; set; }

    /// <summary>6-20 letters/digits. Optional; stored upper-case; must be unique across employees.</summary>
    [RegularExpression(@"^\s*[A-Za-z0-9]{6,20}\s*$", ErrorMessage = "The PassportNo must be 6 to 20 letters or digits.")]
    public string? PassportNo { get; set; }
}
