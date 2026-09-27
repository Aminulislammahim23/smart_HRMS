namespace smartHRMS.Application.Features.EmployeePersonalDetails.Dtos;

public class EmployeePersonalDetailsDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    /// <summary>Read-only here: stored on the employee record and changed through PUT /api/employees/{id}.</summary>
    public DateTime DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? MaritalStatus { get; set; }

    public string? BloodGroup { get; set; }

    public string? Nationality { get; set; }

    public string? NationalId { get; set; }

    public string? PassportNo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
