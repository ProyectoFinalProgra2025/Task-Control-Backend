using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Tarea;

public class RechazarDelegacionDTO
{
    [Required(ErrorMessage = "El motivo de rechazo es obligatorio")]
    [MinLength(10, ErrorMessage = "El motivo debe tener al menos 10 caracteres")]
    [MaxLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres")]
    public string MotivoRechazo { get; set; } = null!;
}
