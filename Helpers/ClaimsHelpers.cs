using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Helpers;

/// <summary>
/// Helper methods for extracting user information from ClaimsPrincipal
/// </summary>
public static class ClaimsHelpers
{
    /// <summary>
    /// Extracts the user ID (Guid) from the ClaimsPrincipal
    /// </summary>
    public static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// Extracts the user ID (Guid) from the ClaimsPrincipal, throws if not found
    /// </summary>
    public static Guid GetUserIdOrThrow(ClaimsPrincipal principal)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
            throw new UnauthorizedAccessException("Usuario no encontrado en el token");
        return userId.Value;
    }

    /// <summary>
    /// Extracts the empresa ID (Guid) from the ClaimsPrincipal
    /// </summary>
    public static Guid? GetEmpresaId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("empresaId");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Extracts the user role from the ClaimsPrincipal as string
    /// </summary>
    public static string? GetRole(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role);
    }

    /// <summary>
    /// Extracts the user role from the ClaimsPrincipal as RolUsuario enum
    /// </summary>
    public static RolUsuario? GetRol(ClaimsPrincipal principal)
    {
        var roleString = GetRole(principal);
        if (string.IsNullOrEmpty(roleString))
            return null;
        return Enum.TryParse<RolUsuario>(roleString, ignoreCase: true, out var rol) ? rol : null;
    }

    /// <summary>
    /// Extracts the user name from the ClaimsPrincipal
    /// </summary>
    public static string? GetUserName(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Name)
               ?? principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
    }

    /// <summary>
    /// Checks if the user has a specific role
    /// </summary>
    public static bool HasRole(ClaimsPrincipal principal, string role)
    {
        var userRole = GetRole(principal);
        return string.Equals(userRole, role, StringComparison.Ordinal);
    }
}
