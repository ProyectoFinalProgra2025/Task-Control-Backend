using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Models
{
    public class Tarea
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;

        public string Titulo { get; set; } = null!;
        public string Descripcion { get; set; } = null!;

        public EstadoTarea Estado { get; set; } = EstadoTarea.Pendiente;
        public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Medium;
        public DateTime? DueDate { get; set; }

        public Departamento? Departamento { get; set; }

        // Asignación
        public Guid? AsignadoAUsuarioId { get; set; }
        public Usuario? AsignadoAUsuario { get; set; }

        // Auditoría básica
        public Guid CreatedByUsuarioId { get; set; }
        public Usuario CreatedByUsuario { get; set; } = null!;

        // Delegación entre jefes de área
        public bool EstaDelegada { get; set; } = false;
        public Guid? DelegadoPorUsuarioId { get; set; }
        public Usuario? DelegadoPorUsuario { get; set; }
        public Guid? DelegadoAUsuarioId { get; set; }
        public Usuario? DelegadoAUsuario { get; set; }
        public DateTime? DelegadaAt { get; set; }
        public bool? DelegacionAceptada { get; set; } // null = pendiente, true = aceptada, false = rechazada
        public string? MotivoRechazoJefe { get; set; }
        public DateTime? DelegacionResueltaAt { get; set; }

        // Evidencias de finalización
        public string? EvidenciaTexto { get; set; } // Texto obligatorio
        public List<string>? EvidenciaArchivoUrls { get; set; } // URLs de imágenes/documentos
        public DateTime? FinalizadaAt { get; set; }
        public string? MotivoCancelacion { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<TareaCapacidadRequerida> CapacidadesRequeridas { get; set; } = new List<TareaCapacidadRequerida>();
    }
}