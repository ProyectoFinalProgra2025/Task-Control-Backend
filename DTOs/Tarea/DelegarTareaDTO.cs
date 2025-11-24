using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Tarea;

public class DelegarTareaDTO
{
    [Required(ErrorMessage = "El ID del jefe destino es requerido")]
    public Guid JefeDestinoId { get; set; }

    [MaxLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
    public string? Comentario { get; set; }
}
