using System.ComponentModel.DataAnnotations;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Users.Dtos;

public class UserDto
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    /// <summary>Employee, Manager, HR or Admin.</summary>
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public Guid? EmployeeId { get; set; }

    public string? EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public DateTime? LastLoginAt { get; set; }

    /// <summary>Set while the account is locked after failed sign-ins.</summary>
    public DateTime? LockoutEndAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class CreateUserDto
{
    [Required]
    [MaxLength(100)]
    [RegularExpression(@"^[A-Za-z0-9._@-]+$", ErrorMessage = "The username may only contain letters, digits and . _ @ -")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(UserRole))]
    public UserRole? Role { get; set; }

    /// <summary>Required for Employee and Manager accounts; optional for HR and Admin.</summary>
    public Guid? EmployeeId { get; set; }
}

public class UpdateUserDto
{
    [Required]
    [EnumDataType(typeof(UserRole))]
    public UserRole? Role { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    /// <summary>Required for Employee and Manager accounts; null unlinks the employee (HR/Admin only).</summary>
    public Guid? EmployeeId { get; set; }
}

public class ResetPasswordDto
{
    [Required]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
