using System.Linq.Expressions;
using smartHRMS.Domain.Common;

namespace smartHRMS.Application.Interfaces;

/// <summary>
/// Data access for profile records that belong to one employee (personal details, addresses, emergency contacts,
/// education, experience). Lookups always take the employee id, so a record is only reachable through its owner.
/// </summary>
public interface IEmployeeOwnedRepository<T> where T : EmployeeOwnedEntity
{
    /// <summary>Returns the record only if it belongs to <paramref name="employeeId"/>.</summary>
    Task<T?> GetByIdAsync(Guid employeeId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every record of this type, for all employees (read-only).</summary>
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>All records of one employee, oldest first.</summary>
    Task<List<T>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);

    Task AddAsync(T entity, CancellationToken cancellationToken);

    void Remove(T entity);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
