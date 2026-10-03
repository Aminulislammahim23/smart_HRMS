using smartHRMS.Application.Features.Users.Dtos;

namespace smartHRMS.Application.Features.Users;

/// <summary>Sign-in account administration. Every method is Admin-only.</summary>
public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken);

    Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken);

    Task ResetPasswordAsync(Guid id, ResetPasswordDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the first Admin account from configuration when no Admin exists yet. Returns false when nothing was
    /// created (an Admin already exists, or no bootstrap credentials are configured).
    /// </summary>
    Task<bool> EnsureBootstrapAdminAsync(string? username, string? password, CancellationToken cancellationToken);
}
