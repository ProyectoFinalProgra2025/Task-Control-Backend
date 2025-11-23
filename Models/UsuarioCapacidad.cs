namespace TaskControlBackend.Models;

public class UsuarioCapacidad
{
    public Guid UsuarioId { get; set; }
    
    public Usuario Usuario { get; set; } = null!;
    public Guid CapacidadId { get; set; }
    public Capacidad Capacidad { get; set; } = null!;
    public int Nivel { get; set; } 
}