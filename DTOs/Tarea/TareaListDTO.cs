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

        // Información de delegación
        public bool EstaDelegada { get; set; }
        public Guid? DelegadoPorUsuarioId { get; set; }
        public string? DelegadoPorUsuarioNombre { get; set; }
        public Guid? DelegadoAUsuarioId { get; set; }
        public string? DelegadoAUsuarioNombre { get; set; }
        public bool? DelegacionAceptada { get; set; }
        public string? MotivoRechazoJefe { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}