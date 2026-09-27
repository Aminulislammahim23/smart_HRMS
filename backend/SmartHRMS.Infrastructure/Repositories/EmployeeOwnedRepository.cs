using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Common;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

/// <summary>One EF Core implementation for every employee-owned profile record type.</summary>
public class EmployeeOwnedRepository<T> : IEmployeeOwnedRepository<T> where T : EmployeeOwnedEntity
{
    private readonly SmartHRMSDbContext _dbContext;

    public EmployeeOwnedRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T?> GetByIdAsync(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<T>()
            .FirstOrDefaultAsync(entity => entity.Id == id && entity.EmployeeId == employeeId, cancellationToken);
    }

    public async Task<List<T>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<T>()
            .Where(entity => entity.EmployeeId == employeeId)
            .OrderBy(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<T>().AnyAsync(predicate, cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        await _dbContext.Set<T>().AddAsync(entity, cancellationToken);
    }

    public void Remove(T entity)
    {
        _dbContext.Set<T>().Remove(entity);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
