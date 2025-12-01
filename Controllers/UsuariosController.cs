using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Usuario;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace TaskControlBackend.Controllers;

[Route("api/[controller]")]
public class UsuariosController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IUsuarioService _svc;
    private readonly BlobService _blobService;

    public UsuariosController(AppDbContext db, IUsuarioService svc, BlobService blobService)
    {
        _db = db;
        _svc = svc;
        _blobService = blobService;
    }

    // PERFIL COMPLETO DEL USUARIO AUTENTICADO (CON CAPACIDADES)
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = GetUserId();

        var dto = await _svc.GetAsync(
            requesterUserId: userId,
            requesterEmpresaId: GetEmpresaId(),
            id: userId,
            requesterIsAdminEmpresa: IsAdminEmpresa(),
            requesterIsAdminGeneral: IsAdminGeneral()
        );

        if (dto is null)
            return NotFound(new { success = false, message = "Usuario no encontrado" });

        return SuccessData(dto);
    }

    // GET /api/usuarios/me/dashboard - Dashboard personal con estadísticas
    [HttpGet("me/dashboard")]
    public async Task<IActionResult> MiDashboard()
    {
        var userId = GetUserId();
        var empresaId = GetEmpresaId();

        if (!empresaId.HasValue)
            return BadRequest(new { success = false, message = "Usuario sin empresa asociada" });

        // Stats de tareas
        var tareasStats = await _db.Tareas
            .Where(t => t.AsignadoAUsuarioId == userId && t.IsActive)
            .GroupBy(t => t.Estado)
            .Select(g => new { Estado = g.Key, Count = g.Count() })
            .ToListAsync();

        var tareasTotal = tareasStats.Sum(s => s.Count);
        var tareasPendientes = tareasStats.FirstOrDefault(s => s.Estado == Models.Enums.EstadoTarea.Pendiente)?.Count ?? 0;
        var tareasAsignadas = tareasStats.FirstOrDefault(s => s.Estado == Models.Enums.EstadoTarea.Asignada)?.Count ?? 0;
        var tareasAceptadas = tareasStats.FirstOrDefault(s => s.Estado == Models.Enums.EstadoTarea.Aceptada)?.Count ?? 0;
        var tareasFinalizadas = tareasStats.FirstOrDefault(s => s.Estado == Models.Enums.EstadoTarea.Finalizada)?.Count ?? 0;

        // Tareas de hoy (due date hoy)
        var hoy = DateTime.UtcNow.Date;
        var tareasHoy = await _db.Tareas
            .Where(t => t.AsignadoAUsuarioId == userId &&
                        t.IsActive &&
                        t.DueDate.HasValue &&
                        t.DueDate.Value.Date == hoy &&
                        (t.Estado == Models.Enums.EstadoTarea.Asignada || t.Estado == Models.Enums.EstadoTarea.Aceptada))
            .CountAsync();

        // Tareas urgentes (prioridad alta)
        var tareasUrgentes = await _db.Tareas
            .CountAsync(t => t.AsignadoAUsuarioId == userId &&
                            t.IsActive &&
                            t.Prioridad == Models.Enums.PrioridadTarea.High &&
                            (t.Estado == Models.Enums.EstadoTarea.Asignada || t.Estado == Models.Enums.EstadoTarea.Aceptada));

        return SuccessData(new
        {
            tareas = new
            {
                total = tareasTotal,
                pendientes = tareasPendientes,
                asignadas = tareasAsignadas,
                aceptadas = tareasAceptadas,
                finalizadas = tareasFinalizadas,
                hoy = tareasHoy,
                urgentes = tareasUrgentes
            }
        });
    }

    // GET /api/usuarios/me/tareas-recientes - Últimas tareas del usuario
    [HttpGet("me/tareas-recientes")]
    public async Task<IActionResult> MisTareasRecientes([FromQuery] int limit = 10)
    {
        var userId = GetUserId();

        var tareas = await _db.Tareas
            .Include(t => t.CreatedByUsuario)
            .Where(t => t.AsignadoAUsuarioId == userId && t.IsActive)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Take(limit)
            .Select(t => new
            {
                t.Id,
                t.Titulo,
                Estado = t.Estado.ToString(),
                Prioridad = t.Prioridad.ToString(),
                t.DueDate,
                CreatedByNombre = t.CreatedByUsuario.NombreCompleto,
                t.CreatedAt,
                t.UpdatedAt
            })
            .ToListAsync();

        return SuccessData(tareas);
    }

    // CRUD DE USUARIOS (ADMIN EMPRESA / ADMIN GENERAL)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioDTO dto)
    {
        if (!IsAdminEmpresa()) return Forbid();
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        var empresaId = GetEmpresaId();
        if (empresaId is null) return Unauthorized();

        var id = await _svc.CreateAsync(empresaId.Value, dto);
        return StatusCode(201, new { success = true, message = "Usuario creado", data = new { id } });
    }


    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? rol)
    {
        if (!IsAdminEmpresa() && !IsAdminGeneral() && !IsManagerDepartamento()) return Forbid();

        var empresaId = GetEmpresaId();
        if (!IsAdminGeneral() && empresaId is null)
            return Unauthorized();

        var list = await _svc.ListAsync(empresaId ?? Guid.Empty, rol);
        return Ok(new { success = true, data = list });
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var dto = await _svc.GetAsync(
            requesterUserId: GetUserId(),
            requesterEmpresaId: GetEmpresaId(),
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

        var empresaId = GetEmpresaId();
        if (empresaId is null) return Unauthorized();

        await _svc.UpdateAsync(empresaId.Value, id, dto);
        return Ok(new { success = true, message = "Usuario actualizado" });
    }


    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!IsAdminEmpresa()) return Forbid();

        var empresaId = GetEmpresaId();
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

        var empresaId = GetEmpresaId();
        if (empresaId is null) return Unauthorized();

        await _svc.UpdateCapacidadesComoAdminAsync(empresaId.Value, id, dto.Capacidades);

        return Ok(new { success = true, message = "Capacidades del usuario actualizadas" });
    }

    [HttpPut("mis-capacidades")]
    public async Task<IActionResult> UpdateMisCapacidades([FromBody] UpdateCapacidadesDTO dto)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        var empresaId = GetEmpresaId();
        if (empresaId is null) return Unauthorized();

        var userId = GetUserId();

        await _svc.UpdateMisCapacidadesAsync(userId, empresaId.Value, dto.Capacidades);

        return Ok(new { success = true, message = "Tus capacidades han sido actualizadas" });
    }
    
    // DELETE api/usuarios/mis-capacidades/{capacidadId}
    // Todos los roles autenticados pueden usarlo (AdminGeneral, AdminEmpresa, Usuario)
    [HttpDelete("mis-capacidades/{capacidadId:guid}")]
    [Authorize]  // ya está a nivel de controlador
    public async Task<IActionResult> DeleteMiCapacidad([FromRoute] Guid capacidadId)
    {
        var empresaId = GetEmpresaId();
        if (empresaId is null)
            return BadRequest(new { success = false, message = "El usuario no tiene empresa asociada" });

        var userId = GetUserId();

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

    // ==================== FOTO DE PERFIL ====================

    /// <summary>
    /// Sube o actualiza la foto de perfil del usuario autenticado
    /// </summary>
    [HttpPost("me/foto-perfil")]
    public async Task<IActionResult> SubirFotoPerfil(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Archivo vacío" });

        var empresaId = GetEmpresaId();
        if (empresaId is null)
            return BadRequest(new { success = false, message = "El usuario no tiene empresa asociada" });

        var userId = GetUserId();

        try
        {
            var fotoUrl = await _blobService.UploadProfilePhotoAsync(file, userId);
            await _svc.UpdateFotoPerfilAsync(userId, empresaId.Value, fotoUrl);

            return Ok(new 
            { 
                success = true, 
                message = "Foto de perfil actualizada",
                data = new { fotoUrl }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Sube o actualiza la foto de perfil de otro usuario (solo AdminEmpresa)
    /// </summary>
    [HttpPost("{id:guid}/foto-perfil")]
    public async Task<IActionResult> SubirFotoPerfilUsuario(Guid id, IFormFile file)
    {
        if (!IsAdminEmpresa()) return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Archivo vacío" });

        var empresaId = GetEmpresaId();
        if (empresaId is null)
            return Unauthorized();

        try
        {
            var fotoUrl = await _blobService.UploadProfilePhotoAsync(file, id);
            await _svc.UpdateFotoPerfilAsync(id, empresaId.Value, fotoUrl);

            return Ok(new 
            { 
                success = true, 
                message = "Foto de perfil actualizada",
                data = new { fotoUrl }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    // ==================== IMPORTACIÓN MASIVA CSV ====================

    /// <summary>
    /// Importa usuarios desde un archivo CSV (solo AdminEmpresa)
    /// Formato CSV esperado: Email,NombreCompleto,Telefono,Rol,Departamento,NivelHabilidad
    /// </summary>
    [HttpPost("importar-csv")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportarUsuariosCsv([FromForm] ImportarUsuariosCsvDTO dto)
    {
        if (!IsAdminEmpresa()) return Forbid();

        if (dto.ArchivoCSV == null || dto.ArchivoCSV.Length == 0)
            return BadRequest(new { success = false, message = "Archivo CSV vacío" });

        var empresaId = GetEmpresaId();
        if (empresaId is null)
            return Unauthorized();

        try
        {
            // Validar extensión del archivo
            var extension = Path.GetExtension(dto.ArchivoCSV.FileName).ToLower();
            if (extension != ".csv")
                return BadRequest(new { success = false, message = "El archivo debe ser un CSV (.csv)" });

            // Guardar CSV en blob storage para auditoría
            var csvUrl = await _blobService.UploadCsvAsync(dto.ArchivoCSV, empresaId.Value);

            // Procesar el CSV
            using var stream = dto.ArchivoCSV.OpenReadStream();
            var resultado = await _svc.ImportarUsuariosDesdeCsvAsync(
                empresaId.Value, 
                stream, 
                dto.PasswordPorDefecto);

            return Ok(new 
            { 
                success = true, 
                message = $"Importación completada: {resultado.Exitosos} exitosos, {resultado.Fallidos} fallidos",
                data = resultado
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Descarga una plantilla CSV de ejemplo para importación
    /// </summary>
    [HttpGet("importar-csv/plantilla")]
    public IActionResult DescargarPlantillaCsv()
    {
        if (!IsAdminEmpresa()) return Forbid();

        var csvContent = "Email,NombreCompleto,Telefono,Rol,Departamento,NivelHabilidad\n" +
                        "juan.perez@empresa.com,Juan Pérez,+51999888777,Usuario,TI,3\n" +
                        "maria.lopez@empresa.com,María López,+51999777666,ManagerDepartamento,Ventas,5\n" +
                        "carlos.garcia@empresa.com,Carlos García,,Usuario,RRHH,2\n";

        var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
        return File(bytes, "text/csv", "plantilla_usuarios.csv");
    }
}