namespace TaskControlBackend.Models;

public class Capacidad
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UsuarioCapacidad> UsuarioCapacidades { get; set; } = new List<UsuarioCapacidad>();
}