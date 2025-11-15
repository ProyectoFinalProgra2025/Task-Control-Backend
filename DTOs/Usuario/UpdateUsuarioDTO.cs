// DTOs/Usuario/UpdateUsuarioDTO.cs
using System.ComponentModel.DataAnnotations;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.DTOs.Usuario;

public class UpdateUsuarioDTO
{
    [Required] public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
    public Departamento? Departamento { get; set; }
    [Range(1,5)] public int? NivelHabilidad { get; set; }
}