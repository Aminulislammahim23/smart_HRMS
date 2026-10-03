using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.Auth.Dtos;

public class LoginDto
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    [Required]
    [MaxLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Who is signed in. The role decides what the frontend shows; the API checks it again on every request.</summary>
public class CurrentUserDto
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    /// <summary>Employee, Manager, HR or Admin.</summary>
    public string Role { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }

    public string? EmployeeCode { get; set; }

    /// <summary>Employee name, or the username for an account without an employee.</summary>
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginResultDto
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public CurrentUserDto User { get; set; } = new();
}
