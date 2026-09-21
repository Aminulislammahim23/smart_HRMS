using smartHRMS.Domain.Common;

namespace smartHRMS.Domain.Entities;

public class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
