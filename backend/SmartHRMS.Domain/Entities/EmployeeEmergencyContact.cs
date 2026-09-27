using smartHRMS.Domain.Common;

namespace smartHRMS.Domain.Entities;

public class EmployeeEmergencyContact : EmployeeOwnedEntity
{
    public string Name { get; set; } = string.Empty;

    public string Relationship { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }
}
