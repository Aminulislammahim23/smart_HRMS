using smartHRMS.Application.Features.Audit;

namespace smartHRMS.Application.Interfaces;

public interface IAuditLogReader
{
    /// <summary>Newest first, at most <paramref name="take"/> entries.</summary>
    Task<List<AuditLogDto>> SearchAsync(string? entityType, Guid? entityId, string? action, int take, CancellationToken cancellationToken);
}
