using TaskControlBackend.DTOs.Auth;

namespace TaskControlBackend.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDTO> LoginAsync(LoginRequestDTO dto);
    Task<TokenResponseDTO> RefreshAsync(RefreshTokenRequestDTO dto);
    Task LogoutAsync(string refreshToken);
    Task<Guid> RegisterAdminEmpresaAsync(RegisterAdminEmpresaDTO dto);
    Task<Guid> RegisterAdminGeneralAsync(RegisterAdminGeneralDTO dto);

}