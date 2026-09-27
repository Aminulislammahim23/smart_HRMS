using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.Designations.Dtos;

public class CreateDesignationDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
