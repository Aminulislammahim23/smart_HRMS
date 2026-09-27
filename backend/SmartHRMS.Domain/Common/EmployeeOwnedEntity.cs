using smartHRMS.Domain.Entities;

namespace smartHRMS.Domain.Common;

/// <summary>Base for profile records that belong to exactly one employee (addresses, contacts, education, ...).</summary>
public abstract class EmployeeOwnedEntity : BaseEntity
{
    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }
}
