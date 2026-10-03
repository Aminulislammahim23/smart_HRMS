using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Interfaces;

public interface IUserRepository
{
    /// <summary>Tracked, with the employee loaded.</summary>
    Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked, with the employee loaded. Usernames compare case-insensitively (database collation).</summary>
    Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>Read-only, with employees loaded, ordered by username.</summary>
    Task<List<ApplicationUser>> GetAllAsync(CancellationToken cancellationToken);

    Task<bool> UsernameExistsAsync(string username, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> EmployeeHasUserAsync(Guid employeeId, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> AnyAdminAsync(CancellationToken cancellationToken);

    /// <summary>Active Admin accounts other than <paramref name="excludeId"/>.</summary>
    Task<int> CountActiveAdminsAsync(Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Display names (employee name, else username) for the given user ids; unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    Task AddAsync(ApplicationUser user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
