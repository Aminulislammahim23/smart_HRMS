using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<List<Employee>> GetAllAsync(CancellationToken cancellationToken);

    Task<bool> EmployeeCodeExistsAsync(string employeeCode, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
