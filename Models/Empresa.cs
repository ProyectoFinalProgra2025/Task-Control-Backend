using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Models;

public class Empresa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = null!;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public EstadoEmpresa Estado { get; set; } = EstadoEmpresa.Pending;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}