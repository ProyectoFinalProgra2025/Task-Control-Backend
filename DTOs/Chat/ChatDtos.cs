using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TaskControlBackend.DTOs.Chat;

public record CreateOneToOneRequest(int UserId);
public record CreateGroupRequest(string Name, List<int> MemberIds);
public record SendMessageRequest(string Text);
public record AddGroupMemberRequest(int UserId);

public static class ClaimsHelpers
{
    public static int? GetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }

    public static string? GetUserName(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name) ??
        principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
}
