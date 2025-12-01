using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace TaskControlBackend.Hubs;

/// <summary>
/// Custom UserIdProvider for SignalR to properly identify users from JWT claims.
/// This is needed because JWT uses "sub" claim while SignalR defaults to NameIdentifier.
/// </summary>
public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // Try NameIdentifier first (standard claim)
        var userId = connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        
        // Fallback to JWT "sub" claim if NameIdentifier not found
        if (string.IsNullOrEmpty(userId))
        {
            userId = connection.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
        
        // Last resort - try just "sub" as string
        if (string.IsNullOrEmpty(userId))
        {
            userId = connection.User?.FindFirstValue("sub");
        }
        
        return userId;
    }
}
