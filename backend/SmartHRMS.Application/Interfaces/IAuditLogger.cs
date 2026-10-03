namespace smartHRMS.Application.Interfaces;

/// <summary>
/// Writes audit entries for the current user. <see cref="Add"/> only stages the entry: it is saved by the same
/// SaveChanges as the change it describes, so the log and the data can't disagree.
/// </summary>
public interface IAuditLogger
{
    void Add(string action, string entityType, Guid? entityId, string? details = null);

    /// <summary>Saves an entry on its own, for events with no other change (e.g. a failed sign-in).</summary>
    Task WriteAsync(string action, string entityType, Guid? entityId, string? details, Guid? userId, string? username, CancellationToken cancellationToken);
}
