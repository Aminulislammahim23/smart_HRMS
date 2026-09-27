using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class EmployeeDocumentRepository : IEmployeeDocumentRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public EmployeeDocumentRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EmployeeDocument?> GetByIdAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        return await _dbContext.EmployeeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.EmployeeId == employeeId, cancellationToken);
    }

    public async Task<List<EmployeeDocument>> GetByEmployeeIdAsync(Guid employeeId, bool includeInactive, CancellationToken cancellationToken)
    {
        return await _dbContext.EmployeeDocuments
            .AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && (includeInactive || d.IsActive))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken)
    {
        await _dbContext.EmployeeDocuments.AddAsync(document, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
