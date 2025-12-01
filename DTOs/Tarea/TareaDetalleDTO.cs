using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.DTOs.Tarea
{
    public class TareaDetalleDTO
    {
        public Guid Id { get; set; }
        public Guid EmpresaId { get; set; }

        public string Titulo { get; set; } = null!;
        public string Descripcion { get; set; } = null!;

        public EstadoTarea Estado { get; set; }
        public PrioridadTarea Prioridad { get; set; }
        public DateTime? DueDate { get; set; }
        public Departamento? Departamento { get; set; }

        public List<string> CapacidadesRequeridas { get; set; } = new();

        public Guid? AsignadoAUsuarioId { get; set; }
        public string? AsignadoAUsuarioNombre { get; set; }

        // Información del creador para sistema de chat
        public Guid CreatedByUsuarioId { get; set; }
        public string CreatedByUsuarioNombre { get; set; } = null!;

        // Evidencia simple (mantener compatibilidad)
        public string? EvidenciaTexto { get; set; }
        public string? EvidenciaImagenUrl { get; set; }

        // NUEVAS PROPIEDADES: Documentos adjuntos y evidencias múltiples
        public List<DocumentoAdjuntoDTO> DocumentosAdjuntos { get; set; } = new();
        public List<EvidenciaDTO> Evidencias { get; set; } = new();

        // Información de delegación
        public bool EstaDelegada { get; set; }
        public Guid? DelegadoPorUsuarioId { get; set; }
        public string? DelegadoPorUsuarioNombre { get; set; }
        public Guid? DelegadoAUsuarioId { get; set; }
        public string? DelegadoAUsuarioNombre { get; set; }
        public DateTime? DelegadaAt { get; set; }
        public bool? DelegacionAceptada { get; set; }
        public string? MotivoRechazoJefe { get; set; }
        public DateTime? DelegacionResueltaAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? FinalizadaAt { get; set; }
        public string? MotivoCancelacion { get; set; }
    }
}