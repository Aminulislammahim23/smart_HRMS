namespace smartHRMS.Application.Features.Auth;

/// <summary>Sign-in protection, bound from the "Auth" section.</summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Failed sign-ins in a row that lock the account.</summary>
    public int LockoutThreshold { get; set; } = 5;

    /// <summary>How long a locked account stays locked.</summary>
    public int LockoutMinutes { get; set; } = 15;

    public static void EnsureValid(AuthOptions options)
    {
        if (options.LockoutThreshold < 1 || options.LockoutMinutes < 1)
        {
            throw new InvalidOperationException($"{SectionName}:LockoutThreshold and {SectionName}:LockoutMinutes must be 1 or more.");
        }
    }
}
