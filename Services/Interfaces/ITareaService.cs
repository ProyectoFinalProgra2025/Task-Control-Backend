using TaskControlBackend.DTOs.Tarea;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Services.Interfaces
{
    public interface ITareaService
    {
        Task<Guid> CreateAsync(Guid empresaId, Guid adminEmpresaId, CreateTareaDTO dto);

        Task<List<TareaListDTO>> ListAsync(
            Guid empresaId, RolUsuario rol, Guid userId,
            EstadoTarea? estado, PrioridadTarea? prioridad, Departamento? departamento, Guid? asignadoAUsuarioId);

        /// <summary>
        /// Listar solo las tareas ASIGNADAS al usuario (para Workers y Managers en vista "Mis Tareas")
        /// </summary>
        Task<List<TareaListDTO>> ListMisTareasAsync(
            Guid empresaId, Guid userId,
            EstadoTarea? estado, PrioridadTarea? prioridad, Departamento? departamento);

        Task<TareaDetalleDTO?> GetAsync(Guid empresaId, RolUsuario rol, Guid userId, Guid tareaId);

        Task UpdateAsync(Guid empresaId, Guid tareaId, UpdateTareaDTO dto);

        // 🔹 NUEVOS
        Task AsignarManualAsync(Guid empresaId, Guid tareaId, Guid assignedByUserId, AsignarManualTareaDTO dto);
        Task AsignarAutomaticamenteAsync(Guid empresaId, Guid tareaId, bool forzarReasignacion);

        Task AceptarAsync(Guid empresaId, Guid tareaId, Guid usuarioId);
        Task FinalizarAsync(Guid empresaId, Guid tareaId, Guid usuarioId, FinalizarTareaDTO dto);
        Task CancelarAsync(Guid empresaId, Guid tareaId, Guid adminEmpresaId, string? motivo);

        // Reasignar tarea (diferente de asignación inicial)
        Task ReasignarAsync(Guid empresaId, Guid tareaId, Guid adminEmpresaId, Guid? nuevoUsuarioId, bool asignacionAutomatica, string? motivo = null);

        // Historial de asignaciones
        Task<List<TareaAsignacionHistorialDTO>> GetHistorialAsignacionesAsync(Guid tareaId);

        // 🔹 DELEGACIÓN ENTRE JEFES
        Task DelegarTareaAJefeAsync(Guid empresaId, Guid tareaId, Guid jefeOrigenId, DelegarTareaDTO dto);
        Task AceptarDelegacionAsync(Guid empresaId, Guid tareaId, Guid jefeDestinoId, AceptarDelegacionDTO dto);
        Task RechazarDelegacionAsync(Guid empresaId, Guid tareaId, Guid jefeDestinoId, RechazarDelegacionDTO dto);

        // ==================== DOCUMENTOS Y EVIDENCIAS ====================
        
        /// <summary>
        /// Agrega un documento adjunto a una tarea
        /// </summary>
        Task<DocumentoAdjuntoDTO> AgregarDocumentoAdjuntoAsync(
            Guid empresaId, Guid tareaId, Guid usuarioId,
            string nombreArchivo, string archivoUrl, string tipoMime, long tamanoBytes);

        /// <summary>
        /// Agrega una evidencia a una tarea (puede ser texto, archivo o ambos)
        /// </summary>
        Task<EvidenciaDTO> AgregarEvidenciaAsync(
            Guid empresaId, Guid tareaId, Guid usuarioId,
            string? descripcion, string? nombreArchivo, string? archivoUrl, string? tipoMime, long tamanoBytes);

        /// <summary>
        /// Obtiene los documentos adjuntos de una tarea
        /// </summary>
        Task<List<DocumentoAdjuntoDTO>> GetDocumentosAdjuntosAsync(Guid empresaId, Guid tareaId);

        /// <summary>
        /// Obtiene las evidencias de una tarea
        /// </summary>
        Task<List<EvidenciaDTO>> GetEvidenciasAsync(Guid empresaId, Guid tareaId);

        /// <summary>
        /// Elimina un documento adjunto
        /// </summary>
        Task EliminarDocumentoAdjuntoAsync(Guid empresaId, Guid tareaId, Guid documentoId, Guid usuarioId);

        /// <summary>
        /// Elimina una evidencia
        /// </summary>
        Task EliminarEvidenciaAsync(Guid empresaId, Guid tareaId, Guid evidenciaId, Guid usuarioId);
    }
}