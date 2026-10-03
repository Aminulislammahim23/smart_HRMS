using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Auth;
using smartHRMS.Application.Features.Auth.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>
/// Sign-in. The access token goes in the Authorization header ("Bearer &lt;token&gt;") of every other request. There is
/// no refresh token: sign in again when it expires.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Signs in with username and password. Repeated failures lock the account for a while.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(dto, cancellationToken);
        return Ok(ApiResponse<LoginResultDto>.Ok(result, "Signed in successfully."));
    }

    /// <summary>The signed-in user (role, linked employee).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        return Ok(ApiResponse<CurrentUserDto>.Ok(user, "Current user retrieved successfully."));
    }

    /// <summary>Changes the signed-in user's password. Existing sessions end; sign in again with the new password.</summary>
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        await _authService.ChangePasswordAsync(dto, cancellationToken);
        return Ok(ApiResponse.Ok("Password changed. Please sign in again."));
    }
}
