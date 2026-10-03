using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

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

    public Task<Employee?> GetProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        // Tests attach the profile records to the employee's navigation collections directly.
        return Task.FromResult(Employees.FirstOrDefault(e => e.Id == id));
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.Any(e => e.Id == id));
    }

    public Task<List<Guid>> GetDirectReportIdsAsync(Guid managerId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Employees.Where(e => e.ManagerId == managerId).Select(e => e.Id).ToList());
    }

    public Task<List<Employee>> GetPayrollCandidatesAsync(DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var joinedBefore = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return Task.FromResult(Employees
            .Where(e => (e.Status == EmployeeStatus.Active || e.Status == EmployeeStatus.OnLeave) && e.JoiningDate < joinedBefore)
            .OrderBy(e => e.EmployeeCode)
            .ToList());
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
