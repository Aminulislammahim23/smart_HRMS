using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Tests.Fakes;

public class FakeDesignationRepository : IDesignationRepository
{
    public List<Designation> Designations { get; } = new();

    /// <summary>Employees used for the employee-count queries.</summary>
    public List<Employee> Employees { get; } = new();

    public Task<Designation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Designations.FirstOrDefault(d => d.Id == id));
    }

    public Task<List<Designation>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Designations.OrderBy(d => d.Name).ToList());
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        // SQL Server's default collation is case-insensitive, so mirror that here.
        return Task.FromResult(Designations.Any(d =>
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) && (excludeId == null || d.Id != excludeId)));
    }

    public Task<int> CountEmployeesAsync(Guid designationId, IReadOnlyCollection<EmployeeStatus>? statuses, CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.Count(e =>
            e.DesignationId == designationId && (statuses == null || statuses.Contains(e.Status))));
    }

    public Task<Dictionary<Guid, int>> GetEmployeeCountsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.GroupBy(e => e.DesignationId).ToDictionary(g => g.Key, g => g.Count()));
    }

    public Task AddAsync(Designation designation, CancellationToken cancellationToken)
    {
        Designations.Add(designation);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
