using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Validation;

namespace smartHRMS.Application.Features.EmployeeExperiences.Dtos;

public class CreateEmployeeExperienceDto
{
    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Job title held at that company.</summary>
    [Required]
    [MaxLength(150)]
    public string Designation { get; set; } = string.Empty;

    [NotDefault]
    public DateTime StartDate { get; set; }

    /// <summary>Null means the job is ongoing. Otherwise it can't be before StartDate or in the future.</summary>
    public DateTime? EndDate { get; set; }

    [MaxLength(2000)]
    public string? Responsibilities { get; set; }
}
