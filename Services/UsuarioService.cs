using System.Linq;
using System.Globalization;
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
    public async Task<Guid> CreateAsync(Guid empresaId, CreateUsuarioDTO dto, bool requesterIsAdminGeneral)
    {
        var rol = dto.Rol ?? RolUsuario.Usuario;

        // Validación de seguridad
        if (!requesterIsAdminGeneral)
        {
            // AdminEmpresa solo puede crear Usuario o ManagerDepartamento
            if (rol == RolUsuario.AdminGeneral || rol == RolUsuario.AdminEmpresa)
            {
                throw new UnauthorizedAccessException("No tiene permisos para asignar este rol.");
            }
        }

        if (rol == RolUsuario.ManagerDepartamento && dto.Departamento == null)
        {
            throw new ArgumentException("ManagerDepartamento debe tener un departamento asignado");
        }

        PasswordHasher.CreatePasswordHash(dto.Password, out var hash, out var salt);

        var user = new Usuario
        {
            Email = dto.Email,
            NombreCompleto = dto.NombreCompleto,
            Telefono = dto.Telefono,
            PasswordHash = hash,
            PasswordSalt = salt,
            Rol = rol,
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
        Guid requesterUserId,
        Guid? requesterEmpresaId,
        Guid id,
        bool requesterIsAdminEmpresa,
        bool requesterIsAdminGeneral)
    {
        // Build query with security filters from the start
        var query = _db.Usuarios
            .Include(x => x.UsuarioCapacidades)
                .ThenInclude(uc => uc.Capacidad)
            .Where(x => x.Id == id);

        // Apply empresa filter if requester is not AdminGeneral
        if (!requesterIsAdminGeneral && requesterEmpresaId.HasValue)
        {
            var isOwner = requesterUserId == id;
            // Allow if: (1) owner viewing themselves OR (2) AdminEmpresa viewing their empresa
            if (!isOwner)
                query = query.Where(x => x.EmpresaId == requesterEmpresaId.Value);
        }

        var u = await query.FirstOrDefaultAsync();
        if (u is null) return null;

        return new UsuarioDTO
        {
            Id = u.Id,
            Email = u.Email,
            NombreCompleto = u.NombreCompleto,
            Telefono = u.Telefono,
            Rol = u.Rol.ToString(),
            EmpresaId = u.EmpresaId ?? Guid.Empty,
            Departamento = u.Departamento?.ToString(),
            NivelHabilidad = u.NivelHabilidad,
            FotoPerfilUrl = u.FotoPerfilUrl,
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

    // LISTA TODOS LOS USUARIOS DE UNA EMPRESA (Workers y Managers) con filtro opcional por rol
    public async Task<List<UsuarioListDTO>> ListAsync(Guid empresaId, string? rolFilter = null)
    {
        var query = _db.Usuarios
            .AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && 
                       (u.Rol == RolUsuario.Usuario || u.Rol == RolUsuario.ManagerDepartamento));

        // Aplicar filtro por rol si se especifica
        if (!string.IsNullOrEmpty(rolFilter))
        {
            if (rolFilter.Equals("ManagerDepartamento", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.Rol == RolUsuario.ManagerDepartamento);
            }
            else if (rolFilter.Equals("Usuario", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.Rol == RolUsuario.Usuario);
            }
        }

        query = query.OrderByDescending(u => u.CreatedAt);

        return await EntityFrameworkQueryableExtensions.ToListAsync(
            Queryable.Select(query, u => new UsuarioListDTO
            {
                Id = u.Id,
                NombreCompleto = u.NombreCompleto,
                Email = u.Email,
                Rol = u.Rol.ToString(),
                Departamento = u.Departamento != null ? u.Departamento.ToString() : null,
                NivelHabilidad = u.NivelHabilidad,
                IsActive = u.IsActive
            }));
    }

    // ACTUALIZA LOS DATOS PRINCIPALES DE UN USUARIO
    public async Task UpdateAsync(Guid empresaId, Guid id, UpdateUsuarioDTO dto)
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
    public async Task DeleteAsync(Guid empresaId, Guid id)
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

        // Liberar tareas asignadas o aceptadas del usuario desactivado
        var tareas = await _db.Tareas
            .Where(t => t.AsignadoAUsuarioId == u.Id && (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada))
            .ToListAsync();

        foreach (var tarea in tareas)
        {
            tarea.Estado = EstadoTarea.Pendiente;
            tarea.AsignadoAUsuarioId = null;
        }

        await _db.SaveChangesAsync();
    }

    // CREA O BUSCA UNA CAPACIDAD POR NOMBRE INSENSIBLE A MAYÚSCULAS
    private async Task<Capacidad> GetOrCreateCapacidadAsync(Guid empresaId, string nombre)
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
            var capacidad = await GetOrCreateCapacidadAsync(usuario.EmpresaId ?? Guid.Empty, item.Nombre);

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
        Guid empresaId,
        Guid usuarioId,
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
        Guid usuarioId,
        Guid empresaId,
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
    public async Task DeleteMisCapacidadAsync(Guid usuarioId, Guid empresaId, Guid capacidadId)
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

    // ==================== NUEVOS MÉTODOS ====================

    /// <summary>
    /// Actualiza la foto de perfil de un usuario
    /// </summary>
    public async Task<string> UpdateFotoPerfilAsync(Guid usuarioId, Guid empresaId, string fotoUrl)
    {
        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == usuarioId && u.EmpresaId == empresaId);

        if (usuario is null)
            throw new KeyNotFoundException("Usuario no encontrado");

        usuario.FotoPerfilUrl = fotoUrl;
        usuario.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return fotoUrl;
    }

    /// <summary>
    /// Importa usuarios desde un archivo CSV
    /// Formato esperado: Email,NombreCompleto,Telefono,Rol,Departamento,NivelHabilidad
    /// </summary>
    public async Task<ImportarUsuariosResultadoDTO> ImportarUsuariosDesdeCsvAsync(
        Guid empresaId, 
        Stream csvStream, 
        string? passwordPorDefecto = null)
    {
        var resultado = new ImportarUsuariosResultadoDTO();
        var filas = new List<UsuarioCsvRowDTO>();

        // Leer y parsear el CSV
        using (var reader = new StreamReader(csvStream))
        {
            string? headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
                throw new ArgumentException("El archivo CSV está vacío");

            // Parsear encabezados
            var headers = ParseCsvLine(headerLine);
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                headerMap[headers[i].Trim()] = i;
            }

            // Validar encabezados requeridos
            if (!headerMap.ContainsKey("Email") || !headerMap.ContainsKey("NombreCompleto"))
                throw new ArgumentException("El CSV debe contener al menos las columnas: Email, NombreCompleto");

            int filaNum = 1;
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                filaNum++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseCsvLine(line);
                
                var row = new UsuarioCsvRowDTO
                {
                    Email = GetCsvValue(values, headerMap, "Email") ?? "",
                    NombreCompleto = GetCsvValue(values, headerMap, "NombreCompleto") ?? "",
                    Telefono = GetCsvValue(values, headerMap, "Telefono"),
                    Rol = GetCsvValue(values, headerMap, "Rol") ?? "Usuario",
                    Departamento = GetCsvValue(values, headerMap, "Departamento"),
                    NivelHabilidad = ParseNivelHabilidad(GetCsvValue(values, headerMap, "NivelHabilidad"))
                };

                filas.Add(row);
            }
        }

        // Procesar cada fila
        int filaIndex = 1;
        foreach (var fila in filas)
        {
            filaIndex++;
            var resultadoFila = new ImportarUsuarioResultadoDTO
            {
                Fila = filaIndex,
                Email = fila.Email,
                NombreCompleto = fila.NombreCompleto
            };

            try
            {
                // Validar email
                if (string.IsNullOrWhiteSpace(fila.Email))
                    throw new ArgumentException("Email es requerido");

                if (string.IsNullOrWhiteSpace(fila.NombreCompleto))
                    throw new ArgumentException("NombreCompleto es requerido");

                // Verificar si el email ya existe
                var existeEmail = await _db.Usuarios
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Email.ToLower() == fila.Email.ToLower() && u.EmpresaId == empresaId);

                if (existeEmail)
                    throw new ArgumentException($"El email '{fila.Email}' ya está registrado");

                // Parsear rol
                var rol = ParseRol(fila.Rol);
                
                // Parsear departamento
                Departamento? departamento = ParseDepartamento(fila.Departamento);

                // Validar que ManagerDepartamento tenga departamento
                if (rol == RolUsuario.ManagerDepartamento && departamento == null)
                    throw new ArgumentException("ManagerDepartamento debe tener un departamento asignado");

                // Generar contraseña
                var password = passwordPorDefecto ?? GenerarPasswordAleatorio();

                PasswordHasher.CreatePasswordHash(password, out var hash, out var salt);

                var usuario = new Usuario
                {
                    Email = fila.Email.Trim(),
                    NombreCompleto = fila.NombreCompleto.Trim(),
                    Telefono = fila.Telefono?.Trim(),
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Rol = rol,
                    EmpresaId = empresaId,
                    Departamento = departamento,
                    NivelHabilidad = fila.NivelHabilidad
                };

                _db.Usuarios.Add(usuario);
                await _db.SaveChangesAsync();

                resultadoFila.Exitoso = true;
                resultadoFila.UsuarioId = usuario.Id;
                resultadoFila.PasswordGenerado = passwordPorDefecto == null ? password : null;
                resultado.Exitosos++;
            }
            catch (Exception ex)
            {
                resultadoFila.Exitoso = false;
                resultadoFila.Error = ex.Message;
                resultado.Fallidos++;
            }

            resultado.Resultados.Add(resultadoFila);
            resultado.TotalProcesados++;
        }

        return resultado;
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());

        return result.ToArray();
    }

    private string? GetCsvValue(string[] values, Dictionary<string, int> headerMap, string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out int index) || index >= values.Length)
            return null;
        
        var value = values[index].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private int? ParseNivelHabilidad(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out int nivel) && nivel >= 1 && nivel <= 5)
            return nivel;
        return null;
    }

    private RolUsuario ParseRol(string? rol)
    {
        if (string.IsNullOrWhiteSpace(rol))
            return RolUsuario.Usuario;

        return rol.Trim().ToLower() switch
        {
            "manager" or "managerdepartamento" or "jefe" => RolUsuario.ManagerDepartamento,
            "usuario" or "worker" or "trabajador" => RolUsuario.Usuario,
            _ => RolUsuario.Usuario
        };
    }

    private Departamento? ParseDepartamento(string? depto)
    {
        if (string.IsNullOrWhiteSpace(depto)) return null;

        // Intentar parsear como enum
        if (Enum.TryParse<Departamento>(depto.Trim(), true, out var result))
            return result;

        // Mapeos alternativos
        return depto.Trim().ToLower() switch
        {
            "finanzas" or "finance" or "contabilidad" => Departamento.Finanzas,
            "mantenimiento" or "maintenance" => Departamento.Mantenimiento,
            "produccion" or "production" => Departamento.Produccion,
            "marketing" or "mercadeo" => Departamento.Marketing,
            "logistica" or "logistics" => Departamento.Logistica,
            "ninguno" or "none" => Departamento.Ninguno,
            _ => null
        };
    }

    private string GenerarPasswordAleatorio()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // Cambia la contraseña de un usuario por AdminEmpresa
    public async Task CambiarPasswordPorAdminEmpresaAsync(Guid adminEmpresaId, ChangePasswordAdminEmpresaDTO dto)
    {
        var admin = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == adminEmpresaId && u.Rol == RolUsuario.AdminEmpresa && u.IsActive);
        if (admin == null)
            throw new UnauthorizedAccessException("Solo un AdminEmpresa activo puede realizar esta acción.");

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == dto.UsuarioId && u.EmpresaId == admin.EmpresaId && u.IsActive);
        if (usuario == null)
            throw new KeyNotFoundException("Usuario no encontrado o inactivo en la empresa.");

        PasswordHasher.CreatePasswordHash(dto.NuevaPassword, out var hash, out var salt);
        usuario.PasswordHash = hash;
        usuario.PasswordSalt = salt;
        usuario.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // Cambia la contraseña de un AdminEmpresa por AdminGeneral
    public async Task CambiarPasswordAdminEmpresaPorAdminGeneralAsync(Guid adminGeneralId, ChangePasswordAdminGeneralDTO dto)
    {
        var adminGeneral = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == adminGeneralId && u.Rol == RolUsuario.AdminGeneral && u.IsActive);
        if (adminGeneral == null)
            throw new UnauthorizedAccessException("Solo un AdminGeneral activo puede realizar esta acción.");

        var adminEmpresa = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == dto.UsuarioId && u.Rol == RolUsuario.AdminEmpresa && u.IsActive);
        if (adminEmpresa == null)
            throw new KeyNotFoundException("AdminEmpresa no encontrado o inactivo.");

        PasswordHasher.CreatePasswordHash(dto.NuevaPassword, out var hash, out var salt);
        adminEmpresa.PasswordHash = hash;
        adminEmpresa.PasswordSalt = salt;
        adminEmpresa.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
    }
    // Cambia la contraseña de un usuario por AdminEmpresa
