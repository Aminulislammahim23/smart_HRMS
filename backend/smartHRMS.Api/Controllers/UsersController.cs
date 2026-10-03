using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Users;
using smartHRMS.Application.Features.Users.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>Sign-in accounts and their roles (Employee, Manager, HR, Admin). Admin only.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api/users")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _userService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<List<UserDto>>.Ok(users, "Users retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user, "User retrieved successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create(CreateUserDto dto, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ApiResponse<UserDto>.Ok(user, "User created successfully."));
    }

    /// <summary>Changes role, active flag and linked employee. The user's existing sessions end.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(Guid id, UpdateUserDto dto, CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user, "User updated successfully."));
    }

    /// <summary>Sets a new password and unlocks the account. The user's existing sessions end.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(Guid id, ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        await _userService.ResetPasswordAsync(id, dto, cancellationToken);
        return Ok(ApiResponse.Ok("Password reset successfully."));
    }
}
