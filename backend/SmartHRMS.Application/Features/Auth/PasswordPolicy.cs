using smartHRMS.Application.Common.Exceptions;

namespace smartHRMS.Application.Features.Auth;

/// <summary>Rules every new password must meet.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static void EnsureValid(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength || password.Length > MaxLength)
        {
            throw new BadRequestException($"The password must be {MinLength} to {MaxLength} characters long.");
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            throw new BadRequestException("The password must contain at least one letter and one digit.");
        }
    }
}
