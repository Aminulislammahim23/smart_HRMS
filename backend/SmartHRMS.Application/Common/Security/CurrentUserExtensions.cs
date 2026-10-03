using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Common.Security;

public static class CurrentUserExtensions
{
    public static bool IsAdmin(this ICurrentUser user) => user.Role == UserRole.Admin;

    public static bool IsHrOrAdmin(this ICurrentUser user) => user.Role is UserRole.HR or UserRole.Admin;

    public static bool IsSelf(this ICurrentUser user, Guid employeeId) => user.EmployeeId is { } own && own == employeeId;

    /// <summary>The user id, or 401 when the request isn't signed in (the API normally rejects that earlier).</summary>
    public static Guid RequireUserId(this ICurrentUser user)
    {
        return user.UserId ?? throw new UnauthorizedException("Authentication is required.");
    }

    /// <summary>The signed-in user's own employee id, or 403 when the account isn't linked to an employee.</summary>
    public static Guid RequireEmployeeId(this ICurrentUser user)
    {
        return user.EmployeeId ?? throw new ForbiddenException("Your account is not linked to an employee record.");
    }

    public static void EnsureHrOrAdmin(this ICurrentUser user)
    {
        if (!user.IsHrOrAdmin())
        {
            throw new ForbiddenException("Only HR or Admin users can do this.");
        }
    }

    public static void EnsureAdmin(this ICurrentUser user)
    {
        if (!user.IsAdmin())
        {
            throw new ForbiddenException("Only Admin users can do this.");
        }
    }

    /// <summary>The user's own employee data, or any employee for HR/Admin.</summary>
    public static void EnsureSelfOrHrOrAdmin(this ICurrentUser user, Guid employeeId)
    {
        if (!user.IsSelf(employeeId) && !user.IsHrOrAdmin())
        {
            throw new ForbiddenException("You can only access your own records.");
        }
    }
}
