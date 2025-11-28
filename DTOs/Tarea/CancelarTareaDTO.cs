using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Tarea;

public class CancelarTareaDTO
{
    [MaxLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres")]
    public string? Motivo { get; set; }
}
