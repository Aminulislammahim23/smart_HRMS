using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class DesignationRepository : IDesignationRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public DesignationRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Designation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Designations.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<List<Designation>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Designations
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Designations
            .AnyAsync(d => d.Name == name && (excludeId == null || d.Id != excludeId), cancellationToken);
    }

    public async Task<int> CountEmployeesAsync(Guid designationId, IReadOnlyCollection<EmployeeStatus>? statuses, CancellationToken cancellationToken)
    {
        var employees = _dbContext.Employees.Where(e => e.DesignationId == designationId);

        if (statuses is not null)
        {
            employees = employees.Where(e => statuses.Contains(e.Status));
        }

        return await employees.CountAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, int>> GetEmployeeCountsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .GroupBy(e => e.DesignationId)
            .Select(group => new { DesignationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.DesignationId, x => x.Count, cancellationToken);
    }

    public async Task AddAsync(Designation designation, CancellationToken cancellationToken)
    {
        await _dbContext.Designations.AddAsync(designation, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
