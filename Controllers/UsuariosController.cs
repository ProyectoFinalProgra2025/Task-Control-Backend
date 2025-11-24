using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Usuario;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace TaskControlBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IUsuarioService _svc;

    public UsuariosController(AppDbContext db, IUsuarioService svc)
    {
        _db = db;
        _svc = svc;
    }

    //  NUEVO: PERFIL COMPLETO DEL USUARIO AUTENTICADO (CON CAPACIDADES)
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = UserId();

        var dto = await _svc.GetAsync(
            requesterUserId: userId,
            requesterEmpresaId: EmpresaIdClaim(),
            id: userId,                                // solicita sus propios datos
            requesterIsAdminEmpresa: IsAdminEmpresa(),
            requesterIsAdminGeneral: IsAdminGeneral()
        );

        if (dto is null)
            return NotFound(new { success = false, message = "Usuario no encontrado" });

        return Ok(new { success = true, data = dto });
    }

    // HELPERS DE ROLES Y CLAIMS
    private bool IsAdminGeneral() =>
        string.Equals(
            User.FindFirstValue(ClaimTypes.Role),
            RolUsuario.AdminGeneral.ToString(),
            StringComparison.Ordinal
        );

    private bool IsAdminEmpresa() =>
        string.Equals(
            User.FindFirstValue(ClaimTypes.Role),
            RolUsuario.AdminEmpresa.ToString(),
            StringComparison.Ordinal
        );

    private bool IsManagerDepartamento() =>
        string.Equals(
            User.FindFirstValue(ClaimTypes.Role),
            RolUsuario.ManagerDepartamento.ToString(),
            StringComparison.Ordinal
        );

    private Guid? EmpresaIdClaim()
    {
        var v = User.FindFirst("empresaId")?.Value;
        return Guid.TryParse(v, out var id) ? id : null;
    }

    private Guid UserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.Parse(sub!);
    }
    
    // CRUD DE USUARIOS (ADMIN EMPRESA / ADMIN GENERAL)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioDTO dto)
    {
        if (!IsAdminEmpresa()) return Forbid();
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        var empresaId = EmpresaIdClaim();
        if (empresaId is null) return Unauthorized();

        var id = await _svc.CreateAsync(empresaId.Value, dto);
        return StatusCode(201, new { success = true, message = "Usuario creado", data = new { id } });
    }


    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? rol)
    {
        if (!IsAdminEmpresa() && !IsAdminGeneral() && !IsManagerDepartamento()) return Forbid();

        var empresaId = EmpresaIdClaim();
        if (!IsAdminGeneral() && empresaId is null)
            return Unauthorized();

        var list = await _svc.ListAsync(empresaId ?? Guid.Empty, rol);
        return Ok(new { success = true, data = list });
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var dto = await _svc.GetAsync(
            requesterUserId: UserId(),
            requesterEmpresaId: EmpresaIdClaim(),
            id: id,
            requesterIsAdminEmpresa: IsAdminEmpresa(),
            requesterIsAdminGeneral: IsAdminGeneral()
        );

        if (dto is null)
            return NotFound(new { success = false, message = "Usuario no encontrado" });

        return Ok(new { success = true, data = dto });
    }


    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUsuarioDTO dto)
    {
        if (!IsAdminEmpresa()) return Forbid();
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        var empresaId = EmpresaIdClaim();
        if (empresaId is null) return Unauthorized();

        await _svc.UpdateAsync(empresaId.Value, id, dto);
        return Ok(new { success = true, message = "Usuario actualizado" });
    }


    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!IsAdminEmpresa()) return Forbid();

        var empresaId = EmpresaIdClaim();
        if (empresaId is null) return Unauthorized();

        await _svc.DeleteAsync(empresaId.Value, id);
        return Ok(new { success = true, message = "Usuario desactivado" });
    }
    
    // CAPACIDADES DEL USUARIO
    [HttpPut("{id:guid}/capacidades")]
    public async Task<IActionResult> UpdateCapacidadesUsuario(
        Guid id,
        [FromBody] UpdateCapacidadesDTO dto)
    {
        if (!IsAdminEmpresa()) return Forbid();
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        var empresaId = EmpresaIdClaim();
        if (empresaId is null) return Unauthorized();

        await _svc.UpdateCapacidadesComoAdminAsync(empresaId.Value, id, dto.Capacidades);

        return Ok(new { success = true, message = "Capacidades del usuario actualizadas" });
    }

    [HttpPut("mis-capacidades")]
    public async Task<IActionResult> UpdateMisCapacidades([FromBody] UpdateCapacidadesDTO dto)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        var empresaId = EmpresaIdClaim();
        if (empresaId is null) return Unauthorized();

        var userId = UserId();

        await _svc.UpdateMisCapacidadesAsync(userId, empresaId.Value, dto.Capacidades);

        return Ok(new { success = true, message = "Tus capacidades han sido actualizadas" });
    }
    
    // DELETE api/usuarios/mis-capacidades/{capacidadId}
    // Todos los roles autenticados pueden usarlo (AdminGeneral, AdminEmpresa, Usuario)
    [HttpDelete("mis-capacidades/{capacidadId:guid}")]
    [Authorize]  // ya está a nivel de controlador
    public async Task<IActionResult> DeleteMiCapacidad([FromRoute] Guid capacidadId)
    {
        var empresaId = EmpresaIdClaim();
        if (empresaId is null)
            return BadRequest(new { success = false, message = "El usuario no tiene empresa asociada" });

        var userId = UserId();

        try
        {
            await _svc.DeleteMisCapacidadAsync(userId, empresaId.Value, capacidadId);
            return Ok(new { success = true, message = "Capacidad eliminada de tu perfil" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }


}