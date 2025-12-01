// Services/Interfaces/IUsuarioService.cs
using TaskControlBackend.DTOs.Usuario;

namespace TaskControlBackend.Services.Interfaces;

public interface IUsuarioService
{
    // Permite a AdminEmpresa cambiar la contraseña de usuarios de su empresa
    Task CambiarPasswordPorAdminEmpresaAsync(Guid adminEmpresaId, ChangePasswordAdminEmpresaDTO dto);

    // Permite a AdminGeneral cambiar la contraseña de AdminEmpresa
    Task CambiarPasswordAdminEmpresaPorAdminGeneralAsync(Guid adminGeneralId, ChangePasswordAdminGeneralDTO dto);
    Task<Guid> CreateAsync(Guid empresaId, CreateUsuarioDTO dto);

    Task<UsuarioDTO?> GetAsync(
        Guid requesterUserId,
        Guid? requesterEmpresaId,
        Guid id,
        bool requesterIsAdminEmpresa,
        bool requesterIsAdminGeneral
    );

    Task<List<UsuarioListDTO>> ListAsync(Guid empresaId, string? rolFilter = null);

    Task UpdateAsync(Guid empresaId, Guid id, UpdateUsuarioDTO dto);

    // Soft delete = IsActive = false
    Task DeleteAsync(Guid empresaId, Guid id);


    /// Permite a un administrador general o administrador de empresa actualizar
    /// las capacidades de un usuario dentro de una empresa.
    Task UpdateCapacidadesComoAdminAsync(
        Guid empresaId,
        Guid usuarioId,
        List<CapacidadNivelItem> capacidades
    );
    /// Permite al propio usuario actualizar sus capacidades en su empresa.
    Task UpdateMisCapacidadesAsync(
        Guid usuarioId,
        Guid empresaId,
        List<CapacidadNivelItem> capacidades
    );
    //Permite eliminar capacidades creadas para el usuario.
    Task DeleteMisCapacidadAsync(
        Guid usuarioId, 
        Guid empresaId, 
        Guid capacidadId);

}