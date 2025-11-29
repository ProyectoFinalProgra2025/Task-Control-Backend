namespace TaskControlBackend.Models;

/// <summary>
/// Registro de historial de asignaciones/reasignaciones de una tarea
/// </summary>
public class TareaAsignacionHistorial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TareaId { get; set; }

    /// <summary>
    /// Usuario al que se asignó la tarea
    /// </summary>
    public Guid? AsignadoAUsuarioId { get; set; }

    /// <summary>
    /// Usuario que realizó la asignación (puede ser admin, jefe, o el sistema en caso de auto-asignación)
    /// </summary>
    public Guid? AsignadoPorUsuarioId { get; set; }

    /// <summary>
    /// Tipo de asignación: Manual, Automatica, Reasignacion, Delegacion
    /// </summary>
    public TipoAsignacion TipoAsignacion { get; set; }

    /// <summary>
    /// Motivo de la reasignación (opcional)
    /// </summary>
    public string? Motivo { get; set; }

    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Tarea Tarea { get; set; } = null!;
    public Usuario? AsignadoAUsuario { get; set; }
    public Usuario? AsignadoPorUsuario { get; set; }
}

public enum TipoAsignacion
{
    Manual = 1,
    Automatica = 2,
    Reasignacion = 3,
    Delegacion = 4
}
