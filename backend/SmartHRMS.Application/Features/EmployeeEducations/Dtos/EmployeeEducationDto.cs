namespace smartHRMS.Application.Features.EmployeeEducations.Dtos;

public class EmployeeEducationDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public string Degree { get; set; } = string.Empty;

    public string Institution { get; set; } = string.Empty;

    public string? Major { get; set; }

    public string? Result { get; set; }

    public int PassingYear { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
