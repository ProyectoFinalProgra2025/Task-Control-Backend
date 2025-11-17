using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Empresa;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;
using TaskControlBackend.Filters;

namespace TaskControlBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmpresasController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IEmpresaService _svc;

        public EmpresasController(AppDbContext db, IEmpresaService svc)
        {
            _db = db;
            _svc = svc;
        }

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

        private int? EmpresaIdClaim()
        {
            var v = User.FindFirst("empresaId")?.Value;
            if (int.TryParse(v, out var id))
                return id;
            return null;
        }

        [HttpGet]
        [AuthorizeRole(RolUsuario.AdminGeneral)]
        public async Task<IActionResult> List([FromQuery] string? estado = null)
        {
            var query = _db.Empresas.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(estado) &&
                Enum.TryParse<EstadoEmpresa>(estado, true, out var st))
            {
                query = query.Where(e => e.Estado == st);
            }

            var list = await query
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new EmpresaListDTO
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    Estado = e.Estado.ToString(),
                    CreatedAt = e.CreatedAt
                }).ToListAsync();

            return Ok(new { success = true, data = list });
        }

        [HttpPut("{id:int}/aprobar")]
        [AuthorizeRole(RolUsuario.AdminGeneral)]
        public async Task<IActionResult> Aprobar([FromRoute] int id)
        {
            await _svc.AprobarAsync(id);
            return Ok(new { success = true, message = "Empresa aprobada exitosamente" });
        }

        [HttpPut("{id:int}/rechazar")]
        [AuthorizeRole(RolUsuario.AdminGeneral)]
        public async Task<IActionResult> Rechazar([FromRoute] int id)
        {
            await _svc.RechazarAsync(id);
            return Ok(new { success = true, message = "Empresa rechazada exitosamente" });
        }

        // GET /api/empresas/{id}/estadisticas
        [HttpGet("{id:int}/estadisticas")]
        [Authorize]
        public async Task<IActionResult> Estadisticas([FromRoute] int id)
        {
            var empresa = await _db.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (empresa is null)
                return NotFound();

            // Autorización
            if (!IsAdminGeneral())
            {
                if (!IsAdminEmpresa())
                    return Forbid();

                var empresaToken = EmpresaIdClaim();
                if (!empresaToken.HasValue || empresaToken.Value != id)
                    return Forbid();
            }

            // Total de trabajadores
            var totalTrabajadores = await _db.Usuarios
                .CountAsync(u => u.EmpresaId == id && u.Rol == RolUsuario.Usuario);

            // Trabajadores activos
            var trabajadoresActivos = await _db.Usuarios
                .CountAsync(u => u.EmpresaId == id &&
                                 u.Rol == RolUsuario.Usuario &&
                                 u.IsActive);

            // ============================
            // NUEVAS ESTADÍSTICAS DE TAREAS
            // ============================

            var totalTareas = await _db.Tareas
                .CountAsync(t => t.EmpresaId == id && t.IsActive);

            var tareasPendientes = await _db.Tareas
                .CountAsync(t => t.EmpresaId == id &&
                                 t.Estado == EstadoTarea.Pendiente &&
                                 t.IsActive);

            var tareasAsignadas = await _db.Tareas
                .CountAsync(t => t.EmpresaId == id &&
                                 t.Estado == EstadoTarea.Asignada &&
                                 t.IsActive);

            var tareasAceptadas = await _db.Tareas
                .CountAsync(t => t.EmpresaId == id &&
                                 t.Estado == EstadoTarea.Aceptada &&
                                 t.IsActive);

            var tareasFinalizadas = await _db.Tareas
                .CountAsync(t => t.EmpresaId == id &&
                                 t.Estado == EstadoTarea.Finalizada &&
                                 t.IsActive);

            var tareasCanceladas = await _db.Tareas
                .CountAsync(t => t.EmpresaId == id &&
                                 t.Estado == EstadoTarea.Cancelada &&
                                 t.IsActive);

            var dto = new EmpresaEstadisticasDTO
            {
                EmpresaId = id,
                NombreEmpresa = empresa.Nombre,
                TotalTrabajadores = totalTrabajadores,
                TrabajadoresActivos = trabajadoresActivos,

                // nuevas estadísticas
                TotalTareas = totalTareas,
                TareasPendientes = tareasPendientes,
                TareasAsignadas = tareasAsignadas,
                TareasAceptadas = tareasAceptadas,
                TareasFinalizadas = tareasFinalizadas,
                TareasCanceladas = tareasCanceladas
            };

            return Ok(new { success = true, data = dto });
        }

        [HttpDelete("{id:int}")]
        [AuthorizeRole(RolUsuario.AdminGeneral)]
        public async Task<IActionResult> HardDelete([FromRoute] int id)
        {
            await _svc.HardDeleteAsync(id);
            return Ok(new
            {
                success = true,
                message = "Empresa y todos sus datos relacionados han sido eliminados definitivamente"
            });
        }
    }
}