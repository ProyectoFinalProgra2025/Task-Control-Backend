namespace TaskControlBackend.DTOs.Usuario;

public class UsuarioPerfilDTO
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
    public string Rol { get; set; } = null!;
    public int? EmpresaId { get; set; }
}