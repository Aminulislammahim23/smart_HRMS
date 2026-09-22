using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Tests.Fakes;

public class FakeEmployeeRepository : IEmployeeRepository
{
    public List<Employee> Employees { get; } = new();

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.FirstOrDefault(e => e.Id == id));
    }

    public Task<List<Employee>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.ToList());
    }

    public Task<bool> EmployeeCodeExistsAsync(string employeeCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.Any(e => e.EmployeeCode == employeeCode && (excludeId == null || e.Id != excludeId)));
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.Any(e => e.Email == email && (excludeId == null || e.Id != excludeId)));
    }

    public Task AddAsync(Employee employee, CancellationToken cancellationToken)
    {
        Employees.Add(employee);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
