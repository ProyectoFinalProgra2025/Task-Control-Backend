using System.ComponentModel.DataAnnotations;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.DTOs.Tarea
{
    public class CreateTareaDTO
    {
        [Required]
        public string Titulo { get; set; } = null!;

        [Required]
        public string Descripcion { get; set; } = null!;

        [Required]
        public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Medium;

        public DateTime? DueDate { get; set; }

        public Departamento? Departamento { get; set; }

        // Nombres de capacidades requeridas (ej: "Buen diseñador")
        public List<string> CapacidadesRequeridas { get; set; } = new();
    }
}