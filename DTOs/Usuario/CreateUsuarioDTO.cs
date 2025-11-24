// DTOs/Usuario/CreateUsuarioDTO.cs
using System.ComponentModel.DataAnnotations;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.DTOs.Usuario;

public class CreateUsuarioDTO
{
    [Required, EmailAddress] public string Email { get; set; } = null!;
    [Required, MinLength(8)] public string Password { get; set; } = null!;
    [Required] public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }

    // Rol: Usuario (3) o ManagerDepartamento (4). AdminEmpresa no puede crear AdminGeneral ni AdminEmpresa
    public RolUsuario? Rol { get; set; }
    public Departamento? Departamento { get; set; }
    [Range(1,5)] public int? NivelHabilidad { get; set; }
}