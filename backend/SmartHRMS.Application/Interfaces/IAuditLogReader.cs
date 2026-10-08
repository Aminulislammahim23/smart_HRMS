using smartHRMS.Application.Features.Audit;

namespace smartHRMS.Application.Interfaces;

/// <summary>Validated audit log criteria; null means "no restriction".</summary>
public sealed record AuditLogFilter(
    IReadOnlyCollection<string>? EntityTypes,
    Guid? EntityId,
    string? Action,
    string? Username,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Take);

public interface IAuditLogReader
{
    /// <summary>Newest first, at most <see cref="AuditLogFilter.Take"/> entries.</summary>
    Task<List<AuditLogDto>> SearchAsync(AuditLogFilter filter, CancellationToken cancellationToken);
}
