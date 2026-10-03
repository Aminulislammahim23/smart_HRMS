using smartHRMS.Application.Features.Auth.Dtos;

namespace smartHRMS.Application.Features.Auth;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken);

    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken);

    Task ChangePasswordAsync(ChangePasswordDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// True while the token's account may still be used: it exists, is active, its employee (if any) still works
    /// here, and the security stamp in the token is current. Checked on every authenticated request.
    /// </summary>
    Task<bool> IsSessionValidAsync(Guid userId, string? securityStamp, CancellationToken cancellationToken);
}
