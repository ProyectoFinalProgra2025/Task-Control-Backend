using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
    public byte[] PasswordHash { get; set; } = null!;
    public byte[] PasswordSalt { get; set; } = null!;
    public RolUsuario Rol { get; set; }

    public int? EmpresaId { get; set; }
    //public Empresa? Empresa { get; set; }

    // NUEVOS CAMPOS PARA TRABAJADOR
    public Departamento? Departamento { get; set; }      // De qué área es
    public int? NivelHabilidad { get; set; }             // 1..5, por ejemplo

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    // Si ya agregaste capacidades:
    public ICollection<UsuarioCapacidad> UsuarioCapacidades { get; set; } = new List<UsuarioCapacidad>();
}