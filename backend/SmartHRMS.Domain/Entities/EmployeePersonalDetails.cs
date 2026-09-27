using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// Optional extra personal data (at most one row per employee). Date of birth is not repeated here: it stays on
/// <see cref="Employee"/>, where it is required at hiring time.
/// </summary>
public class EmployeePersonalDetails : EmployeeOwnedEntity
{
    public Gender? Gender { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }

    public BloodGroup? BloodGroup { get; set; }

    public string? Nationality { get; set; }

    public string? NationalId { get; set; }

    public string? PassportNo { get; set; }
}
