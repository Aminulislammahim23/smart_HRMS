using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.Departments.Dtos;

public class CreateDepartmentDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
