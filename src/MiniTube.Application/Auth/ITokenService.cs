using MiniTube.Domain.Entities;

namespace MiniTube.Application.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    DateTime GetRefreshTokenExpiry();
    string Hash(string token);
}
