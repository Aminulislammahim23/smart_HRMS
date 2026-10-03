using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Common.Security;

/// <summary>
/// The signed-in user of the current request, read from the validated access token. Services use it for every
/// ownership and role decision; nothing a client sends in a request body can change it.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Username { get; }

    UserRole? Role { get; }

    /// <summary>The employee this account belongs to, or null for an account without one.</summary>
    Guid? EmployeeId { get; }
}
