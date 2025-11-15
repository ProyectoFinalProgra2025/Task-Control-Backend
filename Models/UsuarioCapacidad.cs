namespace TaskControlBackend.Models;

public class UsuarioCapacidad
{
    public int UsuarioId { get; set; }
    
    public Usuario Usuario { get; set; } = null!;
    public int CapacidadId { get; set; }
    public Capacidad Capacidad { get; set; } = null!;
    public int Nivel { get; set; } 
}