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
    [Route("api/[controller]")]
    public class EmpresasController : BaseController
    {
        private readonly AppDbContext _db;
        private readonly IEmpresaService _svc;

        public EmpresasController(AppDbContext db, IEmpresaService svc)
        {
            _db = db;
            _svc = svc;
        }
        /// <summary>
        /// GET /api/empresas/{id}/trabajadores-ids?departamento=Produccion&disponibles=true
        /// Obtiene información de trabajadores con filtros útiles para asignaciones
        /// </summary>
        [HttpGet("{id:guid}/trabajadores-ids")]
        [Authorize]
        public async Task<IActionResult> GetTrabajadoresIds(
            [FromRoute] Guid id,
            [FromQuery] Departamento? departamento = null,
            [FromQuery] bool disponibles = false,
            [FromQuery] bool incluirCarga = false)
        {
            // 1. Verificar que la empresa exista
            var empresa = await _db.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (empresa is null)
                return NotFound(new { success = false, message = "Empresa no encontrada" });

            // 2. Autorización
            if (!ValidateEmpresaAccess(id))
                return Forbid();

            // 3. Query base de trabajadores
            var query = _db.Usuarios
                .AsNoTracking()
                .Where(u => u.EmpresaId == id &&
                           (u.Rol == RolUsuario.Usuario || u.Rol == RolUsuario.ManagerDepartamento) &&
                           u.IsActive);

            // Filtrar por departamento si se especifica
            if (departamento.HasValue)
                query = query.Where(u => u.Departamento == departamento.Value);

            var trabajadores = await query.ToListAsync();

            // Calcular carga si es necesario
            if (incluirCarga || disponibles)
            {
                var trabajadoresConCarga = new List<object>();
                var maxTareas = 5; // TODO: Obtener de configuración

                // OPTIMIZACIÓN: Cargar todas las cargas en una sola query para evitar N+1
                var trabajadorIdsInterno = trabajadores.Select(t => t.Id).ToList();
                var cargasPorTrabajador = await _db.Tareas
                    .Where(t => t.EmpresaId == id &&
                                t.AsignadoAUsuarioId.HasValue &&
                                trabajadorIdsInterno.Contains(t.AsignadoAUsuarioId.Value) &&
                                (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada))
                    .GroupBy(t => t.AsignadoAUsuarioId)
                    .Select(g => new { UsuarioId = g.Key!.Value, Count = g.Count() })
                    .ToDictionaryAsync(x => x.UsuarioId, x => x.Count);

                foreach (var trabajador in trabajadores)
                {
                    var carga = cargasPorTrabajador.GetValueOrDefault(trabajador.Id, 0);

                    // Si se filtra por disponibles, solo incluir los que tienen espacio
                    if (disponibles && carga >= maxTareas)
                        continue;

                    if (incluirCarga)
                    {
                        trabajadoresConCarga.Add(new
                        {
                            trabajador.Id,
                            trabajador.NombreCompleto,
                            Departamento = trabajador.Departamento?.ToString(),
                            cargaActual = carga,
                            disponible = carga < maxTareas
                        });
                    }
                    else
                    {
                        trabajadoresConCarga.Add(new
                        {
                            trabajador.Id,
                            trabajador.NombreCompleto,
                            Departamento = trabajador.Departamento?.ToString()
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        empresaId = id,
                        totalTrabajadores = trabajadoresConCarga.Count,
                        trabajadores = trabajadoresConCarga
                    }
                });
            }

            // Respuesta simple (solo IDs)
            var trabajadorIds = trabajadores.Select(t => t.Id).ToList();
            return Ok(new
            {
                success = true,
                data = new
                {
                    empresaId = id,
                    totalTrabajadores = trabajadorIds.Count,
                    trabajadoresIds = trabajadorIds
                }
            });
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

        [HttpPut("{id:guid}/aprobar")]
        [AuthorizeRole(RolUsuario.AdminGeneral)]
        public async Task<IActionResult> Aprobar([FromRoute] Guid id)
        {
            await _svc.AprobarAsync(id);
            return Ok(new { success = true, message = "Empresa aprobada exitosamente" });
        }

        [HttpPut("{id:guid}/rechazar")]
        [AuthorizeRole(RolUsuario.AdminGeneral)]
        public async Task<IActionResult> Rechazar([FromRoute] Guid id)
        {
            await _svc.RechazarAsync(id);
            return Ok(new { success = true, message = "Empresa rechazada exitosamente" });
        }

        // GET /api/empresas/{id}/estadisticas
        [HttpGet("{id:guid}/estadisticas")]
        [Authorize]
        public async Task<IActionResult> Estadisticas([FromRoute] Guid id)
        {
            var empresa = await _db.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (empresa is null)
                return NotFound();

            // Autorización
            if (!ValidateEmpresaAccess(id))
                return Forbid();

            // OPTIMIZACIÓN: Query única para trabajadores
            var trabajadoresStats = await _db.Usuarios
                .Where(u => u.EmpresaId == id && u.Rol == RolUsuario.Usuario)
                .GroupBy(u => u.IsActive)
                .Select(g => new { IsActive = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalTrabajadores = trabajadoresStats.Sum(s => s.Count);
            var trabajadoresActivos = trabajadoresStats.FirstOrDefault(s => s.IsActive)?.Count ?? 0;

            // OPTIMIZACIÓN: Query única para todas las estadísticas de tareas
            var tareasStats = await _db.Tareas
                .Where(t => t.EmpresaId == id && t.IsActive)
                .GroupBy(t => t.Estado)
                .Select(g => new { Estado = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalTareas = tareasStats.Sum(s => s.Count);
            var tareasPendientes = tareasStats.FirstOrDefault(s => s.Estado == EstadoTarea.Pendiente)?.Count ?? 0;
            var tareasAsignadas = tareasStats.FirstOrDefault(s => s.Estado == EstadoTarea.Asignada)?.Count ?? 0;
            var tareasAceptadas = tareasStats.FirstOrDefault(s => s.Estado == EstadoTarea.Aceptada)?.Count ?? 0;
            var tareasFinalizadas = tareasStats.FirstOrDefault(s => s.Estado == EstadoTarea.Finalizada)?.Count ?? 0;
            var tareasCanceladas = tareasStats.FirstOrDefault(s => s.Estado == EstadoTarea.Cancelada)?.Count ?? 0;

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

        [HttpDelete("{id:guid}")]
        [AuthorizeRole(RolUsuario.AdminGeneral)]
        public async Task<IActionResult> HardDelete([FromRoute] Guid id)
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