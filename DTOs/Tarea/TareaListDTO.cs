using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.DTOs.Tarea
{
    public class TareaListDTO
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = null!;
        public string Descripcion { get; set; } = null!;

        public EstadoTarea Estado { get; set; }
        public PrioridadTarea Prioridad { get; set; }
        public DateTime? DueDate { get; set; }
        public Departamento? Departamento { get; set; }

        public Guid? AsignadoAUsuarioId { get; set; }
        public string? AsignadoAUsuarioNombre { get; set; }
        
        // Información del creador para sistema de chat
        public Guid CreatedByUsuarioId { get; set; }
        public string CreatedByUsuarioNombre { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}