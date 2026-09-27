using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Interfaces;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<List<Department>> GetAllAsync(CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Counts employees assigned to the department, optionally only those in one of <paramref name="statuses"/>.</summary>
    Task<int> CountEmployeesAsync(Guid departmentId, IReadOnlyCollection<EmployeeStatus>? statuses, CancellationToken cancellationToken);

    /// <summary>Number of employees per department id, in one query. Departments with no employees are absent.</summary>
    Task<Dictionary<Guid, int>> GetEmployeeCountsAsync(CancellationToken cancellationToken);

    Task AddAsync(Department department, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
