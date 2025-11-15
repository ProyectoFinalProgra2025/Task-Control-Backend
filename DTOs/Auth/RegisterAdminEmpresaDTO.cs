using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Auth;

public class RegisterAdminEmpresaDTO
{
    [Required, EmailAddress] public string Email { get; set; } = null!;
    [Required, MinLength(8)] public string Password { get; set; } = null!;
    [Required] public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }

    [Required] public string NombreEmpresa { get; set; } = null!;
    public string? DireccionEmpresa { get; set; }
    public string? TelefonoEmpresa { get; set; }
}