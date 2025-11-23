using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TaskControlBackend.DTOs.Chat;

public record CreateOneToOneRequest(Guid UserId);
public record CreateGroupRequest(string Name, List<Guid> MemberIds);
public record SendMessageRequest(string Text);
public record AddGroupMemberRequest(Guid UserId);

public static class ClaimsHelpers
{
    public static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(raw, out var id) ? id : (Guid?)null;
    }

    public static string? GetUserName(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name) ??
        principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
}
