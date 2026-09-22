using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public EmployeeRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .Include(e => e.Department)
            .Include(e => e.Designation)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<Employee>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .Include(e => e.Department)
            .Include(e => e.Designation)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> EmployeeCodeExistsAsync(string employeeCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .AnyAsync(e => e.EmployeeCode == employeeCode && (excludeId == null || e.Id != excludeId), cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .AnyAsync(e => e.Email == email && (excludeId == null || e.Id != excludeId), cancellationToken);
    }

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken)
    {
        await _dbContext.Employees.AddAsync(employee, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
