using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Tarea;

public class AceptarDelegacionDTO
{
    [MaxLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
    public string? Comentario { get; set; }
}
