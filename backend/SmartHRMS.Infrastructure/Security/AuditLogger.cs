using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Security;

/// <summary>Stores audit entries in the AuditLogs table through the request's DbContext.</summary>
public sealed class AuditLogger : IAuditLogger
{
    private const int MaxDetailsLength = 1000;

    private readonly SmartHRMSDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AuditLogger(SmartHRMSDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public void Add(string action, string entityType, Guid? entityId, string? details = null)
    {
        _dbContext.AuditLogs.Add(Create(action, entityType, entityId, details, _currentUser.UserId, _currentUser.Username));
    }

    public async Task WriteAsync(string action, string entityType, Guid? entityId, string? details, Guid? userId, string? username, CancellationToken cancellationToken)
    {
        _dbContext.AuditLogs.Add(Create(action, entityType, entityId, details, userId, username));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuditLog Create(string action, string entityType, Guid? entityId, string? details, Guid? userId, string? username)
    {
        return new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is { Length: > MaxDetailsLength } ? details[..MaxDetailsLength] : details,
            UserId = userId,
            Username = username is { Length: > 100 } ? username[..100] : username,
        };
    }
}
