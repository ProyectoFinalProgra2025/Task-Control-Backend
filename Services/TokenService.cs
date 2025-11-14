using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Services;

public class TokenService : ITokenService
{
    private readonly byte[] _key;
    private readonly string _issuer;
    private readonly string _audience;

    public TokenService(IConfiguration config)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));

        var secretKey = config["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("JwtSettings:SecretKey no puede estar vacío");

        _key = System.Text.Encoding.UTF8.GetBytes(secretKey);

        _issuer = config["JwtSettings:Issuer"] ?? throw new ArgumentException("JwtSettings:Issuer no puede estar vacío");
        _audience = config["JwtSettings:Audience"] ?? throw new ArgumentException("JwtSettings:Audience no puede estar vacío");
    }

    public string CreateAccessToken(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        if (claims is null) throw new ArgumentNullException(nameof(claims));
        if (expiresAt <= DateTime.UtcNow) throw new ArgumentException("expiresAt debe ser una fecha futura");

        var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string PlainToken, string Hash) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plain = Convert.ToBase64String(bytes);
        var hash = HashRefreshToken(plain);
        return (plain, hash);
    }

    public string HashRefreshToken(string plainToken)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
            throw new ArgumentException("El token a hashear no puede ser null o vacío");

        using var sha256 = SHA256.Create();
        var raw = System.Text.Encoding.UTF8.GetBytes(plainToken);
        var hashed = sha256.ComputeHash(raw);
        return Convert.ToBase64String(hashed);
    }
}