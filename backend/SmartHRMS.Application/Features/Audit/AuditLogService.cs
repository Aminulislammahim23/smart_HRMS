using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Interfaces;

namespace smartHRMS.Application.Features.Audit;

public interface IAuditLogService
{
    Task<List<AuditLogDto>> SearchAsync(AuditLogQueryDto query, CancellationToken cancellationToken);
}

/// <summary>Read access to the audit log, for Admins only.</summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogReader _reader;
    private readonly ICurrentUser _currentUser;

    public AuditLogService(IAuditLogReader reader, ICurrentUser currentUser)
    {
        _reader = reader;
        _currentUser = currentUser;
    }

    public async Task<List<AuditLogDto>> SearchAsync(AuditLogQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var take = query.Take ?? 100;
        if (take is < 1 or > 500)
        {
            throw new BadRequestException("take must be between 1 and 500.");
        }

        return await _reader.SearchAsync(InputText.Optional(query.EntityType), query.EntityId, InputText.Optional(query.Action), take, cancellationToken);
    }
}
