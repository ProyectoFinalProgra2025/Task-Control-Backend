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
                CreatedByUsuarioId = adminEmpresaId
            };

            // Capacidades requeridas
            foreach (var nombre in dto.CapacidadesRequeridas.Select(n => n.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                tarea.CapacidadesRequeridas.Add(new TareaCapacidadRequerida { Nombre = nombre });
            }

            _db.Tareas.Add(tarea);
            await _db.SaveChangesAsync();

            // Asignación manual explícita
            if (dto.AsignadoAUsuarioId.HasValue)
            {
                await AsignarManualAsync(tarea, dto.AsignadoAUsuarioId.Value);
                await _db.SaveChangesAsync();
            }
            else if (dto.AsignacionAutomatica)
            {
                await AsignarAutomaticamenteAsync(tarea);
                await _db.SaveChangesAsync();
            }

            return tarea.Id;
        }

        private async Task AsignarManualAsync(Tarea tarea, int usuarioId)
        {
            // Validar que el usuario pertenece a la empresa y es Usuario
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Id == usuarioId && u.EmpresaId == tarea.EmpresaId && u.Rol == RolUsuario.Usuario && u.IsActive);

            if (usuario is null)
                throw new ArgumentException("Usuario no válido para asignación");

            // Límite de tareas activas
            var activas = await _db.Tareas.CountAsync(t =>
                t.EmpresaId == tarea.EmpresaId &&
                t.AsignadoAUsuarioId == usuarioId &&
                (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada));

            if (activas >= MaxTareasActivasPorUsuario)
                throw new InvalidOperationException("El usuario ya tiene el máximo de tareas activas permitidas");

            tarea.AsignadoAUsuarioId = usuarioId;
            tarea.Estado = EstadoTarea.Asignada;
            tarea.UpdatedAt = DateTime.UtcNow;
        }

        private async Task AsignarAutomaticamenteAsync(Tarea tarea)
        {
            if (tarea.Departamento == null || !tarea.CapacidadesRequeridas.Any())
            {
                // Sin datos suficientes, dejamos en Pendiente
                return;
            }

            var reqCaps = tarea.CapacidadesRequeridas.Select(cr => cr.Nombre.Trim().ToLower()).ToList();

            // Candidatos: usuarios activos, mismo departamento, de la empresa
            var candidatos = await _db.Usuarios
                .Include(u => u.UsuarioCapacidades).ThenInclude(uc => uc.Capacidad)
                .Where(u => u.EmpresaId == tarea.EmpresaId &&
                            u.Rol == RolUsuario.Usuario &&
                            u.IsActive &&
                            u.Departamento == tarea.Departamento)
                .ToListAsync();

            var candidatosValidos = new List<(Usuario usuario, int cargaActual)>();

            foreach (var u in candidatos)
            {
                // Verificar capacidades
                var capsUsuario = u.UsuarioCapacidades
                    .Select(uc => uc.Capacidad.Nombre.Trim().ToLower())
                    .ToHashSet();

                var cumpleCapacidades = reqCaps.All(rc => capsUsuario.Contains(rc));
                if (!cumpleCapacidades) continue;

                // Contar tareas activas
                var activas = await _db.Tareas.CountAsync(t =>
                    t.EmpresaId == tarea.EmpresaId &&
                    t.AsignadoAUsuarioId == u.Id &&
                    (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada));

                if (activas >= MaxTareasActivasPorUsuario) continue;

                candidatosValidos.Add((u, activas));
            }

            if (!candidatosValidos.Any())
            {
                // Nadie cumple → queda Pendiente
                return;
            }

            // Elegir el de menor carga, y si empatan, el de Id más bajo
            var elegido = candidatosValidos
                .OrderBy(c => c.cargaActual)
                .ThenBy(c => c.usuario.Id)
                .First().usuario;

            tarea.AsignadoAUsuarioId = elegido.Id;
            tarea.Estado = EstadoTarea.Asignada;
            tarea.UpdatedAt = DateTime.UtcNow;
        }

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
                // un trabajador solo ve sus tareas
                q = q.Where(t => t.AsignadoAUsuarioId == userId);
            }
            else if (asignadoAUsuarioId.HasValue)
            {
                q = q.Where(t => t.AsignadoAUsuarioId == asignadoAUsuarioId.Value);
            }

            if (estado.HasValue) q = q.Where(t => t.Estado == estado.Value);
            if (prioridad.HasValue) q = q.Where(t => t.Prioridad == prioridad.Value);
            if (departamento.HasValue) q = q.Where(t => t.Departamento == departamento.Value);

            var list = await q
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

            return list;
        }

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

            // Reemplazar capacidades requeridas (esto es de la definición de la tarea, no del usuario)
            t.CapacidadesRequeridas.Clear();
            foreach (var nombre in dto.CapacidadesRequeridas.Select(n => n.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                t.CapacidadesRequeridas.Add(new TareaCapacidadRequerida { Nombre = nombre });
            }

            t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

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
                await AsignarManualAsync(t, nuevoUsuarioId.Value);
            }
            else if (asignacionAutomatica)
            {
                await AsignarAutomaticamenteAsync(t);
            }

            t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
