using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.Models;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    //Guardamos solo el hash de la contraseña
    public Usuario Usuario { get; set; } = null!;

    [MaxLength(256)]
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    // Mantén este valor inicial: define la fecha de creación automáticamente
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRevoked { get; set; } // Inicialización por defecto innecesaria

    public DateTime? RevokedAt { get; set; }

    [MaxLength(256)]
    public string? RevokeReason { get; set; }
}