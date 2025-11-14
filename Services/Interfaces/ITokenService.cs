using System.Security.Claims;

namespace TaskControlBackend.Services.Interfaces;

public interface ITokenService
{
    string CreateAccessToken(IEnumerable<Claim> claims, DateTime expiresAt);
    (string PlainToken, string Hash) CreateRefreshToken();
    string HashRefreshToken(string plainToken);
}