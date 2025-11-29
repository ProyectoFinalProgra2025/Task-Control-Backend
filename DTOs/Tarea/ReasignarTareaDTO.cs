using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Tarea;

public class ReasignarTareaDTO
{
    public Guid? NuevoUsuarioId { get; set; }
    public bool AsignacionAutomatica { get; set; }

    [MaxLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres")]
    public string? Motivo { get; set; }
}
