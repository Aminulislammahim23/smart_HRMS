using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class DesignationRepository : IDesignationRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public DesignationRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Designations.AnyAsync(d => d.Id == id, cancellationToken);
    }
}
