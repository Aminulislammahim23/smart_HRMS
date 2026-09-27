using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.EmployeeEmergencyContacts.Dtos;

public class CreateEmployeeEmergencyContactDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>E.g. Spouse, Father, Sibling.</summary>
    [Required]
    [MaxLength(50)]
    public string Relationship { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    private string? _email;

    /// <summary>Optional. A blank value is treated as "no email".</summary>
    [EmailAddress]
    [MaxLength(200)]
    public string? Email
    {
        get => _email;
        set => _email = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [MaxLength(500)]
    public string? Address { get; set; }
}
