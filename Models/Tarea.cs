using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Models
{
    public class Tarea
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;

        public string Titulo { get; set; } = null!;
        public string Descripcion { get; set; } = null!;

        public EstadoTarea Estado { get; set; } = EstadoTarea.Pendiente;
        public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Medium;
        public DateTime? DueDate { get; set; }

        public Departamento? Departamento { get; set; }

        // Asignación
        public int? AsignadoAUsuarioId { get; set; }
        public Usuario? AsignadoAUsuario { get; set; }

        // Auditoría básica
        public int CreatedByUsuarioId { get; set; }
        public Usuario CreatedByUsuario { get; set; } = null!;

        // Evidencia
        public string? EvidenciaTexto { get; set; }
        public string? EvidenciaImagenUrl { get; set; }
        public DateTime? FinalizadaAt { get; set; }
        public string? MotivoCancelacion { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<TareaCapacidadRequerida> CapacidadesRequeridas { get; set; } = new List<TareaCapacidadRequerida>();
    }
}