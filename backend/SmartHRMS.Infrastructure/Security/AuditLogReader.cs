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

    public async Task<List<AuditLogDto>> SearchAsync(AuditLogFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditLogs.AsNoTracking();

        if (filter.EntityTypes is not null)
        {
            var types = filter.EntityTypes.ToList();
            query = query.Where(a => types.Contains(a.EntityType));
        }

        if (filter.EntityId is not null)
        {
            query = query.Where(a => a.EntityId == filter.EntityId);
        }

        if (filter.Action is not null)
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (filter.Username is not null)
        {
            query = query.Where(a => a.Username != null && a.Username.Contains(filter.Username));
        }

        if (filter.FromUtc is not null)
        {
            query = query.Where(a => a.CreatedAt >= filter.FromUtc);
        }

        if (filter.ToUtc is not null)
        {
            query = query.Where(a => a.CreatedAt < filter.ToUtc);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(filter.Take)
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
