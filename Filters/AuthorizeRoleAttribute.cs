using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Filters;

public class AuthorizeRoleAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    private readonly RolUsuario[] _roles;
    public AuthorizeRoleAttribute(params RolUsuario[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var claimRole = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (claimRole is null || !_roles.Select(r => r.ToString()).Contains(claimRole))
        {
            context.Result = new ForbidResult();
        }
    }
}