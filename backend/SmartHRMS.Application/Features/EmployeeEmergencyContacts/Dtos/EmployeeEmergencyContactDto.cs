namespace smartHRMS.Application.Features.EmployeeEmergencyContacts.Dtos;

public class EmployeeEmergencyContactDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Relationship { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
