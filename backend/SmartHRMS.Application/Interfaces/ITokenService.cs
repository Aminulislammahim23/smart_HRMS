using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Interfaces;

public interface ITokenService
{
    /// <summary>Issues a signed access token for the user; returns it with its UTC expiry.</summary>
    (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user);
}
