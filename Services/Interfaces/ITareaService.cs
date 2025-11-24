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

        Task<TareaDetalleDTO?> GetAsync(Guid empresaId, RolUsuario rol, Guid userId, Guid tareaId);

        Task UpdateAsync(Guid empresaId, Guid tareaId, UpdateTareaDTO dto);

        // 🔹 NUEVOS
        Task AsignarManualAsync(Guid empresaId, Guid tareaId, AsignarManualTareaDTO dto);
        Task AsignarAutomaticamenteAsync(Guid empresaId, Guid tareaId, bool forzarReasignacion);

        Task AceptarAsync(Guid empresaId, Guid tareaId, Guid usuarioId);
        Task FinalizarAsync(Guid empresaId, Guid tareaId, Guid usuarioId, FinalizarTareaDTO dto);
        Task CancelarAsync(Guid empresaId, Guid tareaId, Guid adminEmpresaId, string? motivo);

        // Si quieres seguir teniendo un "reasignar" genérico, puede reutilizar las anteriores:
        Task ReasignarAsync(Guid empresaId, Guid tareaId, Guid adminEmpresaId, Guid? nuevoUsuarioId, bool asignacionAutomatica);

        // 🔹 DELEGACIÓN ENTRE JEFES
        Task DelegarTareaAJefeAsync(Guid empresaId, Guid tareaId, Guid jefeOrigenId, DelegarTareaDTO dto);
        Task AceptarDelegacionAsync(Guid empresaId, Guid tareaId, Guid jefeDestinoId, AceptarDelegacionDTO dto);
        Task RechazarDelegacionAsync(Guid empresaId, Guid tareaId, Guid jefeDestinoId, RechazarDelegacionDTO dto);
    }
}