using System.ComponentModel.DataAnnotations;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.EmployeeAddresses.Dtos;

public class CreateEmployeeAddressDto
{
    /// <summary>Present or Permanent. An employee can have at most one of each.</summary>
    [Required]
    [EnumDataType(typeof(AddressType))]
    public AddressType? AddressType { get; set; }

    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string District { get; set; } = string.Empty;

    /// <summary>Optional. 3-20 letters, digits, spaces or dashes (e.g. "1207").</summary>
    [RegularExpression(@"^\s*[A-Za-z0-9][A-Za-z0-9 \-]{1,18}[A-Za-z0-9]\s*$", ErrorMessage = "The PostalCode must be 3 to 20 letters, digits, spaces or dashes.")]
    public string? PostalCode { get; set; }
}
