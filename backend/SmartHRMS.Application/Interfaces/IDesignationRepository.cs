using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Interfaces;

public interface IDesignationRepository
{
    Task<Designation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<List<Designation>> GetAllAsync(CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Counts employees assigned to the designation, optionally only those in one of <paramref name="statuses"/>.</summary>
    Task<int> CountEmployeesAsync(Guid designationId, IReadOnlyCollection<EmployeeStatus>? statuses, CancellationToken cancellationToken);

    /// <summary>Number of employees per designation id, in one query. Designations with no employees are absent.</summary>
    Task<Dictionary<Guid, int>> GetEmployeeCountsAsync(CancellationToken cancellationToken);

    Task AddAsync(Designation designation, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
