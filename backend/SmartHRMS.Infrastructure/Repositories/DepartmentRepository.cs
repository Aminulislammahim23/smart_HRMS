using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public DepartmentRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<List<Department>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Departments
            .AnyAsync(d => d.Name == name && (excludeId == null || d.Id != excludeId), cancellationToken);
    }

    public async Task<int> CountEmployeesAsync(Guid departmentId, IReadOnlyCollection<EmployeeStatus>? statuses, CancellationToken cancellationToken)
    {
        var employees = _dbContext.Employees.Where(e => e.DepartmentId == departmentId);

        if (statuses is not null)
        {
            employees = employees.Where(e => statuses.Contains(e.Status));
        }

        return await employees.CountAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, int>> GetEmployeeCountsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .GroupBy(e => e.DepartmentId)
            .Select(group => new { DepartmentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count, cancellationToken);
    }

    public async Task AddAsync(Department department, CancellationToken cancellationToken)
    {
        await _dbContext.Departments.AddAsync(department, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
