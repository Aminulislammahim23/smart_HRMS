using smartHRMS.Domain.Common;

namespace smartHRMS.Domain.Entities;

public class ApplicationUser : BaseEntity
{
    public Guid EmployeeId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Employee? Employee { get; set; }
}
