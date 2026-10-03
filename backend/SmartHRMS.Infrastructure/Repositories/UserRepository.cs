using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public UserRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.ApplicationUsers
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return await _dbContext.ApplicationUsers
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<List<ApplicationUser>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.ApplicationUsers
            .AsNoTracking()
            .Include(u => u.Employee)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(string username, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.ApplicationUsers
            .AnyAsync(u => u.Username == username && (excludeId == null || u.Id != excludeId), cancellationToken);
    }

    public async Task<bool> EmployeeHasUserAsync(Guid employeeId, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.ApplicationUsers
            .AnyAsync(u => u.EmployeeId == employeeId && (excludeId == null || u.Id != excludeId), cancellationToken);
    }

    public async Task<bool> AnyAdminAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.ApplicationUsers.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken);
    }

    public async Task<int> CountActiveAdminsAsync(Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.ApplicationUsers
            .CountAsync(u => u.Role == UserRole.Admin && u.IsActive && (excludeId == null || u.Id != excludeId), cancellationToken);
    }

    public async Task<Dictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var idList = ids.Distinct().ToList();
        var users = await _dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, FirstName = u.Employee != null ? u.Employee.FirstName : null, LastName = u.Employee != null ? u.Employee.LastName : null })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Id, u => u.FirstName is null ? u.Username : $"{u.FirstName} {u.LastName}".Trim());
    }

    public async Task AddAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await _dbContext.ApplicationUsers.AddAsync(user, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
