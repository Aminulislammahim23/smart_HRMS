namespace smartHRMS.Api.Auth;

/// <summary>
/// Access-token settings, bound from the "Jwt" section. The signing key is a secret: keep it in user-secrets or an
/// environment variable (Jwt__SigningKey), never in appsettings.json.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SmartHRMS";

    public string Audience { get; set; } = "SmartHRMS.Web";

    /// <summary>At least 32 characters of random text.</summary>
    public string? SigningKey { get; set; }

    public int AccessTokenMinutes { get; set; } = 60;
}
