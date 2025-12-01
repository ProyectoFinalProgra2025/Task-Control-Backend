using System.ComponentModel.DataAnnotations;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.DTOs.Usuario;

/// <summary>
/// DTO para importación masiva de usuarios desde CSV.
/// Campos esperados en el CSV: Email, NombreCompleto, Telefono, Rol, Departamento, NivelHabilidad
/// </summary>
public class ImportarUsuariosCsvDTO
{
    [Required]
    public IFormFile ArchivoCSV { get; set; } = null!;
    
    /// <summary>
    /// Contraseña por defecto para todos los usuarios creados.
    /// Si no se especifica, se generará una aleatoria.
    /// </summary>
    public string? PasswordPorDefecto { get; set; }
    
    /// <summary>
    /// Si es true, envía email a cada usuario con sus credenciales.
    /// Por defecto false.
    /// </summary>
    public bool EnviarCredencialesPorEmail { get; set; } = false;
}

/// <summary>
/// Resultado de la importación de un usuario individual
/// </summary>
public class ImportarUsuarioResultadoDTO
{
    public int Fila { get; set; }
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
    public Guid? UsuarioId { get; set; }
    public string? PasswordGenerado { get; set; } // Solo si se generó automáticamente
}

/// <summary>
/// Resultado completo de la importación masiva
/// </summary>
public class ImportarUsuariosResultadoDTO
{
    public int TotalProcesados { get; set; }
    public int Exitosos { get; set; }
    public int Fallidos { get; set; }
    public List<ImportarUsuarioResultadoDTO> Resultados { get; set; } = new();
}

/// <summary>
/// DTO interno para parseo de cada fila del CSV
/// </summary>
public class UsuarioCsvRowDTO
{
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
    public string Rol { get; set; } = "Usuario"; // Usuario o ManagerDepartamento
    public string? Departamento { get; set; }
    public int? NivelHabilidad { get; set; }
}
