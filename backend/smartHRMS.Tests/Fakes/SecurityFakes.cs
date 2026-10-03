using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Tests.Fakes;

/// <summary>The signed-in user, set by each test.</summary>
public class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => UserId is not null;

    public Guid? UserId { get; set; }

    public string? Username { get; set; }

    public UserRole? Role { get; set; }

    public Guid? EmployeeId { get; set; }

    public static FakeCurrentUser As(UserRole role, Guid? employeeId = null)
    {
        return new FakeCurrentUser { UserId = Guid.NewGuid(), Username = role.ToString().ToLowerInvariant(), Role = role, EmployeeId = employeeId };
    }

    public void SignInAs(UserRole role, Guid? employeeId = null)
    {
        UserId = Guid.NewGuid();
        Username = role.ToString().ToLowerInvariant();
        Role = role;
        EmployeeId = employeeId;
    }
}

public class FakeAuditLogger : IAuditLogger
{
    public List<(string Action, string EntityType, Guid? EntityId, string? Details)> Entries { get; } = new();

    public void Add(string action, string entityType, Guid? entityId, string? details = null)
    {
        Entries.Add((action, entityType, entityId, details));
    }

    public Task WriteAsync(string action, string entityType, Guid? entityId, string? details, Guid? userId, string? username, CancellationToken cancellationToken)
    {
        Entries.Add((action, entityType, entityId, details));
        return Task.CompletedTask;
    }
}

/// <summary>Readable, deterministic "hash" so tests don't spend time on PBKDF2.</summary>
public class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
}

public class FakeTokenService : ITokenService
{
    public (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user)
    {
        return ($"token-for-{user.Username}", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}

public class FakeUserRepository : IUserRepository
{
    public List<ApplicationUser> Users { get; } = new();

    public int SaveCount { get; private set; }

    public Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<List<ApplicationUser>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Users.OrderBy(u => u.Username).ToList());

    public Task<bool> UsernameExistsAsync(string username, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) && u.Id != excludeId));

    public Task<bool> EmployeeHasUserAsync(Guid employeeId, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Any(u => u.EmployeeId == employeeId && u.Id != excludeId));

    public Task<bool> AnyAdminAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Users.Any(u => u.Role == UserRole.Admin));

    public Task<int> CountActiveAdminsAsync(Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Count(u => u.Role == UserRole.Admin && u.IsActive && u.Id != excludeId));

    public Task<Dictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(Users.Where(u => set.Contains(u.Id)).ToDictionary(u => u.Id, u => u.Username));
    }

    public Task AddAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
