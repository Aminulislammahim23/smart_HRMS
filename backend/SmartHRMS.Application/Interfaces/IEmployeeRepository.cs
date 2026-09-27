using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<List<Employee>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The employee with department, designation and every profile record (personal details, addresses, emergency
    /// contacts, education, experience, active documents) loaded read-only, for the profile view.
    /// </summary>
    Task<Employee?> GetProfileAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> EmployeeCodeExistsAsync(string employeeCode, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
