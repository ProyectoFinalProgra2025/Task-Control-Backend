using System.ComponentModel.DataAnnotations;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.DTOs.Tarea
{
    public class CreateTareaDTO
    {
        [Required(ErrorMessage = "El título es requerido")]
        [MaxLength(200, ErrorMessage = "El título no puede exceder 200 caracteres")]
        public string Titulo { get; set; } = null!;

        [Required(ErrorMessage = "La descripción es requerida")]
        [MaxLength(2000, ErrorMessage = "La descripción no puede exceder 2000 caracteres")]
        public string Descripcion { get; set; } = null!;

        [Required]
        public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Medium;

        public DateTime? DueDate { get; set; }

        public Departamento? Departamento { get; set; }

        // Nombres de capacidades requeridas (ej: "Buen diseñador")
        public List<string> CapacidadesRequeridas { get; set; } = new();
    }
}