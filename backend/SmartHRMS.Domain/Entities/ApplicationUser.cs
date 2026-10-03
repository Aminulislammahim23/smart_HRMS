using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// A sign-in account. Usually linked to one employee (their own data is what an Employee user can see); an
/// administrator account may exist without one.
/// </summary>
public class ApplicationUser : BaseEntity
{
    public Guid? EmployeeId { get; set; }

    public string Username { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash with its own salt and parameters; never the password itself.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Employee;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Changes whenever the password, role or active flag changes. Issued tokens carry it, so such a change ends every
    /// existing session at its next request.
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public int FailedLoginCount { get; set; }

    /// <summary>Sign-in is refused until this UTC time after too many failed attempts.</summary>
    public DateTime? LockoutEndAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public Employee? Employee { get; set; }
}
