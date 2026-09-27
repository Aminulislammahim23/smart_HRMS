using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.Designations.Dtos;

public class UpdateDesignationDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Nullable so that omitting it is a validation error instead of silently deactivating the designation.</summary>
    [Required]
    public bool? IsActive { get; set; }
}
