using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.EmployeeEducations.Dtos;

public class CreateEmployeeEducationDto
{
    /// <summary>Free text: SSC, HSC, Bachelor, Master, Diploma, ...</summary>
    [Required]
    [MaxLength(100)]
    public string Degree { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Institution { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Major { get; set; }

    /// <summary>E.g. "GPA 5.00", "CGPA 3.75", "First Class".</summary>
    [MaxLength(50)]
    public string? Result { get; set; }

    /// <summary>Required. Not before the employee's birth year and at most 5 years ahead (expected results).</summary>
    [Required]
    [Range(1900, 2200)]
    public int? PassingYear { get; set; }
}
