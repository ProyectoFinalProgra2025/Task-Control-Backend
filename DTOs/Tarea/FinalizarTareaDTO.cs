using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Tarea
{
    public class FinalizarTareaDTO
    {
        [Required]
        public string EvidenciaTexto { get; set; } = null!;

        public string? EvidenciaImagenUrl { get; set; }
    }
}