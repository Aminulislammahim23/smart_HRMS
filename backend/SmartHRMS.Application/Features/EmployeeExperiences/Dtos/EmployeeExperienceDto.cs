namespace smartHRMS.Application.Features.EmployeeExperiences.Dtos;

public class EmployeeExperienceDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>True when EndDate is null (the job is ongoing).</summary>
    public bool IsCurrent { get; set; }

    public string? Responsibilities { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
