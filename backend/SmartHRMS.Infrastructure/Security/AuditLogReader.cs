using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Features.Audit;
using smartHRMS.Application.Interfaces;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Security;

public sealed class AuditLogReader : IAuditLogReader
{
    private readonly SmartHRMSDbContext _dbContext;

    public AuditLogReader(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AuditLogDto>> SearchAsync(string? entityType, Guid? entityId, string? action, int take, CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditLogs.AsNoTracking();

        if (entityType is not null)
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (entityId is not null)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (action is not null)
        {
            query = query.Where(a => a.Action == action);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                OccurredAt = a.CreatedAt,
                UserId = a.UserId,
                Username = a.Username,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details,
            })
            .ToListAsync(cancellationToken);
    }
}
