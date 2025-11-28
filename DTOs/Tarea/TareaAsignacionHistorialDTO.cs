using TaskControlBackend.Models;

namespace TaskControlBackend.DTOs.Tarea;

public class TareaAsignacionHistorialDTO
{
    public Guid Id { get; set; }
    public Guid? AsignadoAUsuarioId { get; set; }
    public string? AsignadoAUsuarioNombre { get; set; }
    public Guid? AsignadoPorUsuarioId { get; set; }
    public string? AsignadoPorUsuarioNombre { get; set; }
    public string TipoAsignacion { get; set; } = null!;
    public string? Motivo { get; set; }
    public DateTime FechaAsignacion { get; set; }
}
