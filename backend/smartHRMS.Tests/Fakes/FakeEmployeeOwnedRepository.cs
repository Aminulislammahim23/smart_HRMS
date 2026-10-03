using System.Linq.Expressions;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Common;

namespace smartHRMS.Tests.Fakes;

/// <summary>In-memory stand-in for every employee-owned profile record repository.</summary>
public class FakeEmployeeOwnedRepository<T> : IEmployeeOwnedRepository<T> where T : EmployeeOwnedEntity
{
    public List<T> Items { get; } = new();

    public Task<T?> GetByIdAsync(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.EmployeeId == employeeId));
    }

    public Task<List<T>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.ToList());
    }

    public Task<List<T>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.Where(x => x.EmployeeId == employeeId).OrderBy(x => x.CreatedAt).ToList());
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.Any(predicate.Compile()));
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Remove(T entity)
    {
        Items.Remove(entity);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
