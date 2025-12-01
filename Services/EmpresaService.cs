using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Empresa;
using TaskControlBackend.Hubs;
using TaskControlBackend.Models;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TaskControlBackend.Services
{
    public class EmpresaService : IEmpresaService
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<TareaHub> _hubContext;
        private readonly IConfiguration _config;

        public EmpresaService(AppDbContext db, IHubContext<TareaHub> hubContext, IConfiguration config)
        {
            _db = db;
            _hubContext = hubContext;
            _config = config;
        }

        private int MaxTareasActivasPorUsuario =>
            int.TryParse(_config["AppSettings:MaxTareasActivasPorUsuario"], out var v) ? v : 5;

        // Obtener empresa por Id (solo lectura)
        public Task<Empresa?> GetByIdAsync(Guid id) =>
            _db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

        // Verifica si una empresa está aprobada
        public async Task<bool> EmpresaEstaAprobadaAsync(Guid empresaId)
        {
            var e = await _db.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == empresaId);

            return e is not null && e.Estado == EstadoEmpresa.Approved;
        }

        // Crear empresa con estado Pending
        public async Task<Guid> CrearEmpresaPendingAsync(string nombre, string? dir, string? tel)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ArgumentException("El nombre de la empresa es obligatorio", nameof(nombre));

            var e = new Empresa
            {
                Nombre = nombre.Trim(),
                Direccion = dir?.Trim(),
                Telefono = tel?.Trim(),
                Estado = EstadoEmpresa.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Empresas.Add(e);
            await _db.SaveChangesAsync();

            // Emit SignalR event for new empresa
            await _hubContext.Clients.Group("super_admin").SendAsync("empresa:created", new
            {
                id = e.Id,
                nombre = e.Nombre,
                estado = e.Estado.ToString(),
                createdAt = e.CreatedAt
            });

            return e.Id;
        }

        // Aprobar empresa
        public async Task AprobarAsync(Guid empresaId)
        {
            var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId)
                ?? throw new KeyNotFoundException($"Empresa con Id {empresaId} no encontrada");

            e.Estado = EstadoEmpresa.Approved;
            e.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Emit SignalR event for empresa approval
            await _hubContext.Clients.Group("super_admin").SendAsync("empresa:approved", new
            {
                id = e.Id,
                nombre = e.Nombre,
                estado = e.Estado.ToString(),
                updatedAt = e.UpdatedAt
            });
        }

        // Rechazar empresa
        public async Task RechazarAsync(Guid empresaId, string? motivo = null)
        {
            var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId)
                ?? throw new KeyNotFoundException($"Empresa con Id {empresaId} no encontrada");

            e.Estado = EstadoEmpresa.Rejected;
            e.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Emit SignalR event for empresa rejection
            await _hubContext.Clients.Group("super_admin").SendAsync("empresa:rejected", new
            {
                id = e.Id,
                nombre = e.Nombre,
                estado = e.Estado.ToString(),
                updatedAt = e.UpdatedAt
            });

            // TODO: registrar motivo en tabla de auditoría si se implementa
        }

        // Eliminación total de la empresa y todas sus relaciones
        public async Task HardDeleteAsync(Guid empresaId)
        {
            var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId);
            if (empresa is null)
                throw new KeyNotFoundException("Empresa no encontrada");

            // Usar transacción para garantizar atomicidad
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Usuarios asociados
                var usuarios = await _db.Usuarios
                    .Where(u => u.EmpresaId == empresaId)
                    .ToListAsync();

                var userIds = usuarios.Select(u => u.Id).ToList();

                // RefreshTokens de esos usuarios
                var tokens = await _db.RefreshTokens
                    .Where(rt => userIds.Contains(rt.UsuarioId))
                    .ToListAsync();

                // Capacidades de la empresa
                var capacidades = await _db.Capacidades
                    .Where(c => c.EmpresaId == empresaId)
                    .ToListAsync();

                var capacidadIds = capacidades.Select(c => c.Id).ToList();

                // Relación Usuario-Capacidad
                var usuarioCapacidades = await _db.UsuarioCapacidades
                    .Where(uc => capacidadIds.Contains(uc.CapacidadId))
                    .ToListAsync();

                // Tareas de la empresa
                var tareas = await _db.Tareas
                    .Where(t => t.EmpresaId == empresaId)
                    .ToListAsync();

                var tareaIds = tareas.Select(t => t.Id).ToList();

                // Capacidades requeridas de tareas
                var tareaCapacidades = await _db.Set<TareaCapacidadRequerida>()
                    .Where(tc => tareaIds.Contains(tc.TareaId))
                    .ToListAsync();

                // Historial de asignaciones
                var historialAsignaciones = await _db.TareasAsignacionesHistorial
                    .Where(h => tareaIds.Contains(h.TareaId))
                    .ToListAsync();

                // Eliminación en orden para mantener integridad referencial
                _db.TareasAsignacionesHistorial.RemoveRange(historialAsignaciones);
                _db.Set<TareaCapacidadRequerida>().RemoveRange(tareaCapacidades);
                _db.Tareas.RemoveRange(tareas);
                _db.UsuarioCapacidades.RemoveRange(usuarioCapacidades);
                _db.Capacidades.RemoveRange(capacidades);
                _db.RefreshTokens.RemoveRange(tokens);
                _db.Usuarios.RemoveRange(usuarios);
                _db.Empresas.Remove(empresa);

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Obtener información de cola de trabajadores
        public async Task<List<TrabajadorColaDTO>> GetTrabajadoresConColaAsync(Guid empresaId, Departamento? departamento = null)
        {
            // Obtener trabajadores
            var query = _db.Usuarios
                .AsNoTracking()
                .Where(u => u.EmpresaId == empresaId &&
                           (u.Rol == RolUsuario.Usuario || u.Rol == RolUsuario.ManagerDepartamento) &&
                           u.IsActive);

            // Filtrar por departamento si se especifica
            if (departamento.HasValue)
            {
                query = query.Where(u => u.Departamento == departamento.Value);
            }

            var trabajadores = await query.ToListAsync();
            var trabajadorIds = trabajadores.Select(t => t.Id).ToList();

            // Obtener conteo de tareas activas por trabajador
            var tareasActivasPorTrabajador = await _db.Tareas
                .Where(t => t.EmpresaId == empresaId &&
                           t.AsignadoAUsuarioId.HasValue &&
                           trabajadorIds.Contains(t.AsignadoAUsuarioId.Value) &&
                           (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada))
                .GroupBy(t => t.AsignadoAUsuarioId)
                .Select(g => new { UsuarioId = g.Key!.Value, Count = g.Count() })
                .ToDictionaryAsync(x => x.UsuarioId, x => x.Count);

            // Mapear a DTO
            var resultado = trabajadores.Select(t =>
            {
                var tareasActivas = tareasActivasPorTrabajador.GetValueOrDefault(t.Id, 0);
                return new TrabajadorColaDTO
                {
                    Id = t.Id,
                    NombreCompleto = t.NombreCompleto,
                    Email = t.Email,
                    Departamento = t.Departamento?.ToString(),
                    NivelHabilidad = t.NivelHabilidad,
                    TareasActivas = tareasActivas,
                    LimiteMaximo = MaxTareasActivasPorUsuario,
                    Disponible = tareasActivas < MaxTareasActivasPorUsuario
                };
            }).ToList();

            return resultado;
        }
    }
}