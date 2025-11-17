using TaskControlBackend.DTOs.Tarea;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Services.Interfaces
{
    public interface ITareaService
    {
        Task<int> CreateAsync(int empresaId, int adminEmpresaId, CreateTareaDTO dto);

        Task<List<TareaListDTO>> ListAsync(
            int empresaId, RolUsuario rol, int userId,
            EstadoTarea? estado, PrioridadTarea? prioridad, Departamento? departamento, int? asignadoAUsuarioId);

        Task<TareaDetalleDTO?> GetAsync(int empresaId, RolUsuario rol, int userId, int tareaId);

        Task UpdateAsync(int empresaId, int tareaId, UpdateTareaDTO dto);

        // 🔹 NUEVOS
        Task AsignarManualAsync(int empresaId, int tareaId, AsignarManualTareaDTO dto);
        Task AsignarAutomaticamenteAsync(int empresaId, int tareaId, bool forzarReasignacion);

        Task AceptarAsync(int empresaId, int tareaId, int usuarioId);
        Task FinalizarAsync(int empresaId, int tareaId, int usuarioId, FinalizarTareaDTO dto);
        Task CancelarAsync(int empresaId, int tareaId, int adminEmpresaId, string? motivo);

        // Si quieres seguir teniendo un “reasignar” genérico, puede reutilizar las anteriores:
        Task ReasignarAsync(int empresaId, int tareaId, int adminEmpresaId, int? nuevoUsuarioId, bool asignacionAutomatica);
    }
}