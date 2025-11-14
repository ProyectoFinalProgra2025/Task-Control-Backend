// Services/Interfaces/IUsuarioService.cs
using TaskControlBackend.DTOs.Usuario;

namespace TaskControlBackend.Services.Interfaces;

public interface IUsuarioService
{
    Task<int> CreateAsync(int empresaId, CreateUsuarioDTO dto);

    Task<UsuarioDTO?> GetAsync(
        int requesterUserId,
        int? requesterEmpresaId,
        int id,
        bool requesterIsAdminEmpresa,
        bool requesterIsAdminGeneral
    );

    Task<List<UsuarioListDTO>> ListAsync(int empresaId);

    Task UpdateAsync(int empresaId, int id, UpdateUsuarioDTO dto);

    // Soft delete = IsActive = false
    Task DeleteAsync(int empresaId, int id);


    /// Permite a un administrador general o administrador de empresa actualizar
    /// las capacidades de un usuario dentro de una empresa.
    Task UpdateCapacidadesComoAdminAsync(
        int empresaId,
        int usuarioId,
        List<CapacidadNivelItem> capacidades
    );
    /// Permite al propio usuario actualizar sus capacidades en su empresa.
    Task UpdateMisCapacidadesAsync(
        int usuarioId,
        int empresaId,
        List<CapacidadNivelItem> capacidades
    );
    //Permite eliminar capacidades creadas para el usuario.
    Task DeleteMisCapacidadAsync(
        int usuarioId, 
        int empresaId, 
        int capacidadId);

}