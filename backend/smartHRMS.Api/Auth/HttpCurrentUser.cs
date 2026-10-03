using smartHRMS.Application.Common.Security;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Api.Auth;

/// <summary>Reads the signed-in user from the validated token of the current HTTP request (nobody outside a request).</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public bool IsAuthenticated => _accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid? UserId => ReadGuid(AppClaimTypes.UserId);

    public string? Username => IsAuthenticated ? _accessor.HttpContext!.User.FindFirst(AppClaimTypes.Username)?.Value : null;

    public UserRole? Role =>
        IsAuthenticated && Enum.TryParse<UserRole>(_accessor.HttpContext!.User.FindFirst(AppClaimTypes.Role)?.Value, out var role) && Enum.IsDefined(role)
            ? role
            : null;

    public Guid? EmployeeId => ReadGuid(AppClaimTypes.EmployeeId);

    private Guid? ReadGuid(string claimType)
    {
        return IsAuthenticated && Guid.TryParse(_accessor.HttpContext!.User.FindFirst(claimType)?.Value, out var value) ? value : null;
    }
}
