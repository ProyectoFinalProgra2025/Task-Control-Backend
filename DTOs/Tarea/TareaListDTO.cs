using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.DTOs.Tarea
{
    /// <summary>
    /// DTO optimizado para listados de tareas - solo campos esenciales
    /// </summary>
    public class TareaListDTO
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = null!;
        public string Descripcion { get; set; } = null!;

        public EstadoTarea Estado { get; set; }
        public PrioridadTarea Prioridad { get; set; }
        public DateTime? DueDate { get; set; }
        public Departamento? Departamento { get; set; }

        // Asignación básica
        public Guid? AsignadoAUsuarioId { get; set; }
        public string? AsignadoAUsuarioNombre { get; set; }

        // Creador (útil para filtros y referencias)
        public Guid CreatedByUsuarioId { get; set; }
        public string CreatedByUsuarioNombre { get; set; } = null!;

        // Indicador simple de delegación (si necesitas mostrar un badge/icon)
        public bool EstaDelegada { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
