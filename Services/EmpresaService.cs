using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Data;
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
        public EmpresaService(AppDbContext db) => _db = db;

        // Obtener empresa por Id (solo lectura)
        public Task<Empresa?> GetByIdAsync(int id) =>
            _db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

        // Verifica si una empresa está aprobada
        public async Task<bool> EmpresaEstaAprobadaAsync(int empresaId)
        {
            var e = await _db.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == empresaId);

            return e is not null && e.Estado == EstadoEmpresa.Approved;
        }

        // Crear empresa con estado Pending
        public async Task<int> CrearEmpresaPendingAsync(string nombre, string? dir, string? tel)
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

            return e.Id;
        }

        // Aprobar empresa
        public async Task AprobarAsync(int empresaId)
        {
            var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId)
                ?? throw new KeyNotFoundException($"Empresa con Id {empresaId} no encontrada");

            e.Estado = EstadoEmpresa.Approved;
            e.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        // Rechazar empresa
        public async Task RechazarAsync(int empresaId, string? motivo = null)
        {
            var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId)
                ?? throw new KeyNotFoundException($"Empresa con Id {empresaId} no encontrada");

            e.Estado = EstadoEmpresa.Rejected;
            e.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // TODO: registrar motivo en tabla de auditoría si se implementa
        }

        // Eliminación total de la empresa y todas sus relaciones
        public async Task HardDeleteAsync(int empresaId)
        {
            var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId);
            if (empresa is null)
                throw new KeyNotFoundException("Empresa no encontrada");

            // Usuarios asociados
            var usuarios = await _db.Usuarios
                .Where(u => u.EmpresaId == empresaId)
                .ToListAsync();

            // RefreshTokens de esos usuarios
            var userIds = usuarios.Select(u => u.Id).ToList();
            var tokens = await _db.RefreshTokens
                .Where(rt => userIds.Contains(rt.UsuarioId))
                .ToListAsync();

            // Capacidades de la empresa
            var capacidades = await _db.Capacidades
                .Where(c => c.EmpresaId == empresaId)
                .ToListAsync();

            // Relación Usuario-Capacidad
            var capacidadIds = capacidades.Select(c => c.Id).ToList();
            var usuarioCapacidades = await _db.UsuarioCapacidades
                .Where(uc => capacidadIds.Contains(uc.CapacidadId))
                .ToListAsync();

            // TODO: agregar eliminación de tareas, chats u otras relaciones según implementación

            // Eliminación en orden para mantener integridad referencial
            _db.UsuarioCapacidades.RemoveRange(usuarioCapacidades);
            _db.Capacidades.RemoveRange(capacidades);
            _db.RefreshTokens.RemoveRange(tokens);
            _db.Usuarios.RemoveRange(usuarios);
            _db.Empresas.Remove(empresa);

            await _db.SaveChangesAsync();
        }
    }
}