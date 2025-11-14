using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Usuario;

public class UpdateCapacidadesDTO
{
    [Required]
    public List<CapacidadNivelItem> Capacidades { get; set; } = new();
}