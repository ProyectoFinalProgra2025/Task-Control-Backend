using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Auth;

public class RegisterAdminGeneralDTO
{
    [Required, EmailAddress] public string Email { get; set; } = null!;
    [Required, MinLength(8)] public string Password { get; set; } = null!;
    [Required] public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
}