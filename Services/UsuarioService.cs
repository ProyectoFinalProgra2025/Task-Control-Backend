using System.Linq;
using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Usuario;
using TaskControlBackend.Helpers;
using TaskControlBackend.Models;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Services;

public class UsuarioService : IUsuarioService
{
    private readonly AppDbContext _db;
    public UsuarioService(AppDbContext db) => _db = db;

    // CREA UN NUEVO USUARIO CON CONTRASEÑA HASHEADA
    public async Task<int> CreateAsync(int empresaId, CreateUsuarioDTO dto)
    {
        PasswordHasher.CreatePasswordHash(dto.Password, out var hash, out var salt);

        var user = new Usuario
        {
            Email = dto.Email,
            NombreCompleto = dto.NombreCompleto,
            Telefono = dto.Telefono,
            PasswordHash = hash,
            PasswordSalt = salt,
            Rol = RolUsuario.Usuario,
            EmpresaId = empresaId,
            Departamento = dto.Departamento,
            NivelHabilidad = dto.NivelHabilidad
        };

        _db.Usuarios.Add(user);
        await _db.SaveChangesAsync();
        return user.Id;
    }

    // OBTIENE UN USUARIO POR ID CON SUS CAPACIDADES Y CONTROL DE PERMISOS
    public async Task<UsuarioDTO?> GetAsync(
        int requesterUserId,
        int? requesterEmpresaId,
        int id,
        bool requesterIsAdminEmpresa,
        bool requesterIsAdminGeneral)
    {
        var u = await _db.Usuarios
            .Include(x => x.UsuarioCapacidades)
                .ThenInclude(uc => uc.Capacidad)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (u is null) return null;

        var isOwner = requesterUserId == id;
        if (!requesterIsAdminGeneral)
        {
            if (!isOwner && !(requesterIsAdminEmpresa && requesterEmpresaId == u.EmpresaId))
                throw new UnauthorizedAccessException("No autorizado");
        }

        return new UsuarioDTO
        {
            Id = u.Id,
            Email = u.Email,
            NombreCompleto = u.NombreCompleto,
            Telefono = u.Telefono,
            Rol = u.Rol.ToString(),
            EmpresaId = u.EmpresaId ?? 0,
            Departamento = u.Departamento?.ToString(),
            NivelHabilidad = u.NivelHabilidad,
            IsActive = u.IsActive,
            Capacidades = u.UsuarioCapacidades
                .Select(uc => new CapacidadNivelView
                {
                    CapacidadId = uc.CapacidadId,
                    Nombre = uc.Capacidad.Nombre,
                    Nivel = uc.Nivel
                }).ToList()
        };
    }

    // LISTA TODOS LOS USUARIOS DE UNA EMPRESA
    public async Task<List<UsuarioListDTO>> ListAsync(int empresaId)
    {
        var query = _db.Usuarios
            .AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Rol == RolUsuario.Usuario)
            .OrderByDescending(u => u.CreatedAt);

        return await EntityFrameworkQueryableExtensions.ToListAsync(
            Queryable.Select(query, u => new UsuarioListDTO
            {
                Id = u.Id,
                NombreCompleto = u.NombreCompleto,
                Email = u.Email,
                Departamento = u.Departamento != null ? u.Departamento.ToString() : null,
                NivelHabilidad = u.NivelHabilidad,
                IsActive = u.IsActive
            }));
    }

    // ACTUALIZA LOS DATOS PRINCIPALES DE UN USUARIO
    public async Task UpdateAsync(int empresaId, int id, UpdateUsuarioDTO dto)
    {
        var u = await _db.Usuarios
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.EmpresaId == empresaId &&
                x.Rol == RolUsuario.Usuario);

        if (u is null)
            throw new KeyNotFoundException("Usuario no encontrado");

        u.NombreCompleto = dto.NombreCompleto;
        u.Telefono = dto.Telefono;
        u.Departamento = dto.Departamento;
        u.NivelHabilidad = dto.NivelHabilidad;
        u.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // DESACTIVA UN USUARIO EN VEZ DE ELIMINARLO
    public async Task DeleteAsync(int empresaId, int id)
    {
        var u = await _db.Usuarios
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.EmpresaId == empresaId &&
                x.Rol == RolUsuario.Usuario);

        if (u is null)
            throw new KeyNotFoundException("Usuario no encontrado");

        u.IsActive = false;
        u.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // CREA O BUSCA UNA CAPACIDAD POR NOMBRE INSENSIBLE A MAYÚSCULAS
    private async Task<Capacidad> GetOrCreateCapacidadAsync(int empresaId, string nombre)
    {
        var nombreNorm = nombre.Trim().ToLower(); // <-- Corrección EF Core

        var capacidad = await _db.Capacidades
            .FirstOrDefaultAsync(c =>
                c.EmpresaId == empresaId &&
                c.IsActive &&
                c.Nombre.ToLower() == nombreNorm); // <-- Comparación insensible a mayúsculas

        if (capacidad is not null)
            return capacidad;

        capacidad = new Capacidad
        {
            EmpresaId = empresaId,
            Nombre = nombre.Trim(),
            IsActive = true
        };

        _db.Capacidades.Add(capacidad);
        await _db.SaveChangesAsync();

        return capacidad;
    }

    // ACTUALIZA CAPACIDADES DE UN USUARIO (SE USA TANTO PARA ADMIN COMO USUARIO)
    private async Task UpdateCapacidadesAsync(Usuario usuario, List<CapacidadNivelItem> capacidades)
    {
        foreach (var item in capacidades)
        {
            var capacidad = await GetOrCreateCapacidadAsync(usuario.EmpresaId ?? 0, item.Nombre);

            var existente = usuario.UsuarioCapacidades
                .FirstOrDefault(uc => uc.CapacidadId == capacidad.Id);

            if (existente is null)
            {
                usuario.UsuarioCapacidades.Add(new UsuarioCapacidad
                {
                    UsuarioId = usuario.Id,
                    CapacidadId = capacidad.Id,
                    Nivel = item.Nivel
                });
            }
            else
            {
                existente.Nivel = item.Nivel;
            }
        }

        usuario.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ACTUALIZA CAPACIDADES DE OTRO USUARIO COMO ADMINISTRADOR
    public async Task UpdateCapacidadesComoAdminAsync(
        int empresaId,
        int usuarioId,
        List<CapacidadNivelItem> capacidades)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.UsuarioCapacidades)
                .ThenInclude(uc => uc.Capacidad)
            .FirstOrDefaultAsync(u =>
                u.Id == usuarioId &&
                u.EmpresaId == empresaId &&
                u.Rol == RolUsuario.Usuario);

        if (usuario is null)
            throw new KeyNotFoundException("Usuario no encontrado");

        await UpdateCapacidadesAsync(usuario, capacidades);
    }

    // ACTUALIZA MIS PROPIAS CAPACIDADES COMO USUARIO
    public async Task UpdateMisCapacidadesAsync(
        int usuarioId,
        int empresaId,
        List<CapacidadNivelItem> capacidades)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.UsuarioCapacidades)
                .ThenInclude(uc => uc.Capacidad)
            .FirstOrDefaultAsync(u =>
                u.Id == usuarioId &&
                u.EmpresaId == empresaId);

        if (usuario is null)
            throw new KeyNotFoundException("Usuario no encontrado");

        await UpdateCapacidadesAsync(usuario, capacidades);
    }

    // NUEVO: ELIMINA UNA CAPACIDAD DE UN USUARIO
    public async Task DeleteMisCapacidadAsync(int usuarioId, int empresaId, int capacidadId)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.UsuarioCapacidades)
            .ThenInclude(uc => uc.Capacidad)
            .FirstOrDefaultAsync(u => u.Id == usuarioId && u.EmpresaId == empresaId);

        if (usuario is null)
            throw new KeyNotFoundException("Usuario no encontrado");

        var uc = usuario.UsuarioCapacidades
            .FirstOrDefault(x => x.CapacidadId == capacidadId);

        if (uc is null)
            throw new KeyNotFoundException("Capacidad no encontrada para este usuario");

        usuario.UsuarioCapacidades.Remove(uc);
        usuario.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}