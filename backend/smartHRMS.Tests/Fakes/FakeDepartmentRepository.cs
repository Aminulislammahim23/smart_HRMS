using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Tests.Fakes;

public class FakeDepartmentRepository : IDepartmentRepository
{
    public List<Department> Departments { get; } = new();

    /// <summary>Employees used for the employee-count queries.</summary>
    public List<Employee> Employees { get; } = new();

    public Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Departments.FirstOrDefault(d => d.Id == id));
    }

    public Task<List<Department>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Departments.OrderBy(d => d.Name).ToList());
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        // SQL Server's default collation is case-insensitive, so mirror that here.
        return Task.FromResult(Departments.Any(d =>
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) && (excludeId == null || d.Id != excludeId)));
    }

    public Task<int> CountEmployeesAsync(Guid departmentId, IReadOnlyCollection<EmployeeStatus>? statuses, CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.Count(e =>
            e.DepartmentId == departmentId && (statuses == null || statuses.Contains(e.Status))));
    }

    public Task<Dictionary<Guid, int>> GetEmployeeCountsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.GroupBy(e => e.DepartmentId).ToDictionary(g => g.Key, g => g.Count()));
    }

    public Task AddAsync(Department department, CancellationToken cancellationToken)
    {
        Departments.Add(department);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
