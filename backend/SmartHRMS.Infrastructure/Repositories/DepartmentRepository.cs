using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public DepartmentRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Departments.AnyAsync(d => d.Id == id, cancellationToken);
    }
}
