using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Api.Auth;

/// <summary>Issues HMAC-SHA256 signed JWT access tokens.</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _key;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(JwtOptions options, SymmetricSecurityKey key, TimeProvider timeProvider)
    {
        _options = options;
        _key = key;
        _timeProvider = timeProvider;
    }

    public (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, user.Id.ToString()),
            new(AppClaimTypes.Username, user.Username),
            new(AppClaimTypes.Role, user.Role.ToString()),
            new(AppClaimTypes.SecurityStamp, user.SecurityStamp),
        };

        if (user.EmployeeId is { } employeeId)
        {
            claims.Add(new Claim(AppClaimTypes.EmployeeId, employeeId.ToString()));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
