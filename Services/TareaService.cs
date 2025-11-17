using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Tarea;
using TaskControlBackend.Models;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Services
{
    public class TareaService : ITareaService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public TareaService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        private int MaxTareasActivasPorUsuario =>
            int.TryParse(_config["AppSettings:MaxTareasActivasPorUsuario"], out var v) ? v : 5;

        // ============================================================
        // 5.1  CREAR TAREA (NO ASIGNA)
        // ============================================================
        public async Task<int> CreateAsync(int empresaId, int adminEmpresaId, CreateTareaDTO dto)
        {
            var tarea = new Tarea
            {
                EmpresaId = empresaId,
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                Prioridad = dto.Prioridad,
                DueDate = dto.DueDate,
                Departamento = dto.Departamento,
                CreatedByUsuarioId = adminEmpresaId,
                Estado = EstadoTarea.Pendiente
            };

            foreach (var nombre in dto.CapacidadesRequeridas.Select(n => n.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                tarea.CapacidadesRequeridas.Add(new TareaCapacidadRequerida { Nombre = nombre });
            }

            _db.Tareas.Add(tarea);
            await _db.SaveChangesAsync();
            return tarea.Id;
        }

        // ============================================================
        // 5.2  ASIGNACIÓN MANUAL
        // ============================================================
        public async Task AsignarManualAsync(int empresaId, int tareaId, AsignarManualTareaDTO dto)
        {
            var tarea = await _db.Tareas
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (tarea is null)
                throw new KeyNotFoundException("Tarea no encontrada");

            if (tarea.Estado == EstadoTarea.Finalizada || tarea.Estado == EstadoTarea.Cancelada)
                throw new InvalidOperationException("No se pueden asignar tareas finalizadas o canceladas");

            Usuario? usuario = null;

            // Buscar por ID
            if (dto.UsuarioId.HasValue)
            {
                usuario = await _db.Usuarios.FirstOrDefaultAsync(u =>
                    u.Id == dto.UsuarioId.Value &&
                    u.EmpresaId == empresaId &&
                    u.Rol == RolUsuario.Usuario &&
                    u.IsActive);
            }
            // Buscar por nombre
            else if (!string.IsNullOrWhiteSpace(dto.NombreUsuario))
            {
                var nombreBuscado = dto.NombreUsuario.Trim().ToLower();

                var candidatos = await _db.Usuarios
                    .Where(u => u.EmpresaId == empresaId &&
                                u.Rol == RolUsuario.Usuario &&
                                u.IsActive &&
                                u.NombreCompleto.ToLower().Contains(nombreBuscado))
                    .ToListAsync();

                if (!candidatos.Any())
                    throw new KeyNotFoundException("No se encontró ningún usuario con ese nombre");

                if (candidatos.Count > 1)
                    throw new InvalidOperationException("Existe más de un usuario con ese nombre; use UsuarioId.");

                usuario = candidatos.Single();
            }
            else
            {
                throw new ArgumentException("Debe enviarse UsuarioId o NombreUsuario");
            }

            if (usuario is null)
                throw new KeyNotFoundException("Usuario no válido para asignación");

            // Validaciones de departamento y skills
            if (!dto.IgnorarValidacionesSkills)
            {
                if (tarea.Departamento.HasValue &&
                    usuario.Departamento.HasValue &&
                    tarea.Departamento.Value != usuario.Departamento.Value)
                {
                    throw new InvalidOperationException("El usuario no pertenece al departamento requerido por la tarea");
                }

                var reqCaps = tarea.CapacidadesRequeridas
                    .Select(c => c.Nombre.Trim().ToLower())
                    .ToList();

                if (reqCaps.Any())
                {
                    var capsUsuario = await _db.UsuarioCapacidades
                        .Where(uc => uc.UsuarioId == usuario.Id)
                        .Include(uc => uc.Capacidad)
                        .Select(uc => uc.Capacidad.Nombre.Trim().ToLower())
                        .ToListAsync();

                    var cumple = reqCaps.All(rc => capsUsuario.Contains(rc));
                    if (!cumple)
                        throw new InvalidOperationException("El usuario no cumple todas las capacidades requeridas");
                }
            }

            // Límite de tareas activas
            var activas = await _db.Tareas.CountAsync(t =>
                t.EmpresaId == empresaId &&
                t.AsignadoAUsuarioId == usuario.Id &&
                (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada));

            if (activas >= MaxTareasActivasPorUsuario)
                throw new InvalidOperationException("El usuario ya tiene el máximo de tareas activas");

            tarea.AsignadoAUsuarioId = usuario.Id;
            tarea.Estado = EstadoTarea.Asignada;
            tarea.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        // ============================================================
        // 5.3  ASIGNACIÓN AUTOMÁTICA PÚBLICA
        // ============================================================
        public async Task AsignarAutomaticamenteAsync(int empresaId, int tareaId, bool forzarReasignacion)
        {
            var tarea = await _db.Tareas
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (tarea is null)
                throw new KeyNotFoundException("Tarea no encontrada");

            if (!forzarReasignacion && tarea.AsignadoAUsuarioId != null &&
                (tarea.Estado == EstadoTarea.Asignada || tarea.Estado == EstadoTarea.Aceptada))
            {
                throw new InvalidOperationException("La tarea ya tiene un usuario asignado");
            }

            if (forzarReasignacion)
            {
                tarea.AsignadoAUsuarioId = null;
                tarea.Estado = EstadoTarea.Pendiente;
            }

            await AsignarAutomaticamenteInternoAsync(tarea);
            await _db.SaveChangesAsync();
        }

        private async Task AsignarAutomaticamenteInternoAsync(Tarea tarea)
        {
            if (tarea.Departamento == null || !tarea.CapacidadesRequeridas.Any())
                return;

            var reqCaps = tarea.CapacidadesRequeridas
                .Select(cr => cr.Nombre.Trim().ToLower())
                .ToList();

            var candidatos = await _db.Usuarios
                .Include(u => u.UsuarioCapacidades).ThenInclude(uc => uc.Capacidad)
                .Where(u => u.EmpresaId == tarea.EmpresaId &&
                            u.Rol == RolUsuario.Usuario &&
                            u.IsActive &&
                            u.Departamento == tarea.Departamento)
                .ToListAsync();

            var candidatosValidos = new List<(Usuario usuario, int carga)>();

            foreach (var u in candidatos)
            {
                var capsUsuario = u.UsuarioCapacidades
                    .Select(uc => uc.Capacidad.Nombre.Trim().ToLower())
                    .ToHashSet();

                if (!reqCaps.All(rc => capsUsuario.Contains(rc)))
                    continue;

                var activas = await _db.Tareas.CountAsync(t =>
                    t.EmpresaId == tarea.EmpresaId &&
                    t.AsignadoAUsuarioId == u.Id &&
                    (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada));

                if (activas >= MaxTareasActivasPorUsuario)
                    continue;

                candidatosValidos.Add((u, activas));
            }

            if (!candidatosValidos.Any())
                return;

            var elegido = candidatosValidos
                .OrderBy(c => c.carga)
                .ThenBy(c => c.usuario.Id)
                .First().usuario;

            tarea.AsignadoAUsuarioId = elegido.Id;
            tarea.Estado = EstadoTarea.Asignada;
            tarea.UpdatedAt = DateTime.UtcNow;
        }

        // ============================================================
        // LISTAR TAREAS
        // ============================================================
        public async Task<List<TareaListDTO>> ListAsync(
            int empresaId, RolUsuario rol, int userId,
            EstadoTarea? estado, PrioridadTarea? prioridad, Departamento? departamento, int? asignadoAUsuarioId)
        {
            var q = _db.Tareas
                .Include(t => t.AsignadoAUsuario)
                .AsNoTracking()
                .Where(t => t.EmpresaId == empresaId && t.IsActive);

            if (rol == RolUsuario.Usuario)
            {
                q = q.Where(t => t.AsignadoAUsuarioId == userId);
            }
            else if (asignadoAUsuarioId.HasValue)
            {
                q = q.Where(t => t.AsignadoAUsuarioId == asignadoAUsuarioId.Value);
            }

            if (estado.HasValue) q = q.Where(t => t.Estado == estado.Value);
            if (prioridad.HasValue) q = q.Where(t => t.Prioridad == prioridad.Value);
            if (departamento.HasValue) q = q.Where(t => t.Departamento == departamento.Value);

            return await q
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TareaListDTO
                {
                    Id = t.Id,
                    Titulo = t.Titulo,
                    Descripcion = t.Descripcion,
                    Estado = t.Estado,
                    Prioridad = t.Prioridad,
                    DueDate = t.DueDate,
                    Departamento = t.Departamento,
                    AsignadoAUsuarioId = t.AsignadoAUsuarioId,
                    AsignadoAUsuarioNombre = t.AsignadoAUsuario != null ? t.AsignadoAUsuario.NombreCompleto : null,
                    CreatedAt = t.CreatedAt
                }).ToListAsync();
        }

        // ============================================================
        // OBTENER DETALLE
        // ============================================================
        public async Task<TareaDetalleDTO?> GetAsync(int empresaId, RolUsuario rol, int userId, int tareaId)
        {
            var t = await _db.Tareas
                .Include(t => t.AsignadoAUsuario)
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) return null;

            if (rol == RolUsuario.Usuario && t.AsignadoAUsuarioId != userId)
                throw new UnauthorizedAccessException("No autorizado a ver esta tarea");

            return new TareaDetalleDTO
            {
                Id = t.Id,
                EmpresaId = t.EmpresaId,
                Titulo = t.Titulo,
                Descripcion = t.Descripcion,
                Estado = t.Estado,
                Prioridad = t.Prioridad,
                DueDate = t.DueDate,
                Departamento = t.Departamento,
                CapacidadesRequeridas = t.CapacidadesRequeridas.Select(cr => cr.Nombre).ToList(),
                AsignadoAUsuarioId = t.AsignadoAUsuarioId,
                AsignadoAUsuarioNombre = t.AsignadoAUsuario?.NombreCompleto,
                EvidenciaTexto = t.EvidenciaTexto,
                EvidenciaImagenUrl = t.EvidenciaImagenUrl,
                CreatedAt = t.CreatedAt,
                FinalizadaAt = t.FinalizadaAt,
                MotivoCancelacion = t.MotivoCancelacion
            };
        }

        // ============================================================
        // ACTUALIZAR TAREA
        // ============================================================
        public async Task UpdateAsync(int empresaId, int tareaId, UpdateTareaDTO dto)
        {
            var t = await _db.Tareas
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");

            if (t.Estado != EstadoTarea.Pendiente)
                throw new InvalidOperationException("Solo se pueden editar tareas en estado Pendiente");

            t.Titulo = dto.Titulo;
            t.Descripcion = dto.Descripcion;
            t.Prioridad = dto.Prioridad;
            t.DueDate = dto.DueDate;
            t.Departamento = dto.Departamento;

            t.CapacidadesRequeridas.Clear();
            foreach (var nombre in dto.CapacidadesRequeridas.Select(n => n.Trim()))
            {
                t.CapacidadesRequeridas.Add(new TareaCapacidadRequerida { Nombre = nombre });
            }

            t.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        // ============================================================
        // ACEPTAR
        // ============================================================
        public async Task AceptarAsync(int empresaId, int tareaId, int usuarioId)
        {
            var t = await _db.Tareas.FirstOrDefaultAsync(t =>
                t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");
            if (t.AsignadoAUsuarioId != usuarioId)
                throw new UnauthorizedAccessException("No eres el usuario asignado a esta tarea");
            if (t.Estado != EstadoTarea.Asignada)
                throw new InvalidOperationException("Solo se pueden aceptar tareas en estado Asignada");

            t.Estado = EstadoTarea.Aceptada;
            t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // ============================================================
        // FINALIZAR
        // ============================================================
        public async Task FinalizarAsync(int empresaId, int tareaId, int usuarioId, FinalizarTareaDTO dto)
        {
            var t = await _db.Tareas.FirstOrDefaultAsync(t =>
                t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");
            if (t.AsignadoAUsuarioId != usuarioId)
                throw new UnauthorizedAccessException("No eres el usuario asignado a esta tarea");
            if (t.Estado != EstadoTarea.Aceptada)
                throw new InvalidOperationException("Solo se pueden finalizar tareas en estado Aceptada");

            t.Estado = EstadoTarea.Finalizada;
            t.EvidenciaTexto = dto.EvidenciaTexto;
            t.EvidenciaImagenUrl = dto.EvidenciaImagenUrl;
            t.FinalizadaAt = DateTime.UtcNow;
            t.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        // ============================================================
        // CANCELAR
        // ============================================================
        public async Task CancelarAsync(int empresaId, int tareaId, int adminEmpresaId, string? motivo)
        {
            var t = await _db.Tareas.FirstOrDefaultAsync(t =>
                t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");

            if (t.Estado != EstadoTarea.Pendiente && t.Estado != EstadoTarea.Asignada)
                throw new InvalidOperationException("Solo se pueden cancelar tareas Pendientes o Asignadas");

            t.Estado = EstadoTarea.Cancelada;
            t.MotivoCancelacion = motivo;
            t.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        // ============================================================
        // REASIGNAR
        // ============================================================
        public async Task ReasignarAsync(int empresaId, int tareaId, int adminEmpresaId, int? nuevoUsuarioId, bool asignacionAutomatica)
        {
            var t = await _db.Tareas
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");

            if (t.Estado == EstadoTarea.Finalizada || t.Estado == EstadoTarea.Cancelada)
                throw new InvalidOperationException("No se pueden reasignar tareas finalizadas o canceladas");

            t.AsignadoAUsuarioId = null;
            t.Estado = EstadoTarea.Pendiente;

            if (nuevoUsuarioId.HasValue)
            {
                await AsignarManualAsync(empresaId, tareaId, new AsignarManualTareaDTO
                {
                    UsuarioId = nuevoUsuarioId
                });
            }
            else if (asignacionAutomatica)
            {
                await AsignarAutomaticamenteAsync(empresaId, tareaId, false);
            }

            t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
