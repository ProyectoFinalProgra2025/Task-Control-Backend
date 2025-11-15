namespace TaskControlBackend.DTOs.Usuario;

public class UsuarioDTO
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
    public string Rol { get; set; } = null!;
    public int EmpresaId { get; set; }
    public string? Departamento { get; set; }
    public int? NivelHabilidad { get; set; }
    public bool IsActive { get; set; }
    public List<CapacidadNivelView> Capacidades { get; set; } = new();
}

public class CapacidadNivelView
{
    public int CapacidadId { get; set; }
    public string Nombre { get; set; } = null!;
    public int Nivel { get; set; }
}