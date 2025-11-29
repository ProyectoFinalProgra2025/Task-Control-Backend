using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskControlBackend.Helpers;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Controllers;

/// <summary>
/// Base controller with common authentication and authorization helpers
/// </summary>
[ApiController]
[Authorize]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// Gets the current user's ID from the JWT token
    /// </summary>
    protected Guid GetUserId()
    {
        return ClaimsHelpers.GetUserIdOrThrow(User);
    }

    /// <summary>
    /// Gets the current user's ID, returns null if not found
    /// </summary>
    protected Guid? GetUserIdSafe()
    {
        return ClaimsHelpers.GetUserId(User);
    }

    /// <summary>
    /// Gets the current user's empresa ID from the JWT token
    /// </summary>
    protected Guid? GetEmpresaId()
    {
        return ClaimsHelpers.GetEmpresaId(User);
    }

    /// <summary>
    /// Gets the current user's empresa ID, throws if not found
    /// </summary>
    protected Guid GetEmpresaIdOrThrow()
    {
        var empresaId = GetEmpresaId();
        if (!empresaId.HasValue)
            throw new UnauthorizedAccessException("EmpresaId no encontrado en el token");
        return empresaId.Value;
    }

    /// <summary>
    /// Gets the current user's role from the JWT token
    /// </summary>
    protected RolUsuario GetRole()
    {
        var roleString = ClaimsHelpers.GetRole(User);
        return Enum.TryParse<RolUsuario>(roleString, out var role)
            ? role
            : RolUsuario.Usuario;
    }

    /// <summary>
    /// Gets the current user's role as string
    /// </summary>
    protected string? GetRoleString()
    {
        return ClaimsHelpers.GetRole(User);
    }

    /// <summary>
    /// Checks if the current user is AdminGeneral
    /// </summary>
    protected bool IsAdminGeneral()
    {
        return ClaimsHelpers.HasRole(User, RolUsuario.AdminGeneral.ToString());
    }

    /// <summary>
    /// Checks if the current user is AdminEmpresa
    /// </summary>
    protected bool IsAdminEmpresa()
    {
        return ClaimsHelpers.HasRole(User, RolUsuario.AdminEmpresa.ToString());
    }

    /// <summary>
    /// Checks if the current user is ManagerDepartamento
    /// </summary>
    protected bool IsManagerDepartamento()
    {
        return ClaimsHelpers.HasRole(User, RolUsuario.ManagerDepartamento.ToString());
    }

    /// <summary>
    /// Checks if the current user is Usuario (Worker)
    /// </summary>
    protected bool IsUsuario()
    {
        return ClaimsHelpers.HasRole(User, RolUsuario.Usuario.ToString());
    }

    /// <summary>
    /// Validates that the empresaId from the token matches the provided empresaId
    /// </summary>
    protected bool ValidateEmpresaAccess(Guid empresaId)
    {
        if (IsAdminGeneral()) return true;

        var tokenEmpresaId = GetEmpresaId();
        return tokenEmpresaId.HasValue && tokenEmpresaId.Value == empresaId;
    }

    /// <summary>
    /// Returns an error response with consistent format
    /// </summary>
    protected IActionResult Error(string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new { success = false, message });
    }

    /// <summary>
    /// Returns a success response with consistent format
    /// </summary>
    protected IActionResult Success(string message, object? data = null)
    {
        return Ok(new { success = true, message, data });
    }

    /// <summary>
    /// Returns a success response with only data
    /// </summary>
    protected IActionResult SuccessData(object data)
    {
        return Ok(new { success = true, data });
    }
}
