namespace TaskControlBackend.DTOs.Usuario;

public class UsuarioListDTO
{
    public Guid Id { get; set; }
    public string NombreCompleto { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Departamento { get; set; }
    public int? NivelHabilidad { get; set; }
    public bool IsActive { get; set; }
}