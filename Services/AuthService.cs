using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Auth;
using TaskControlBackend.Helpers;
using TaskControlBackend.Models;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace TaskControlBackend.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly ITokenService _tokenSvc;
        private readonly IConfiguration _config;

        public AuthService(AppDbContext db, ITokenService tokenSvc, IConfiguration config)
        {
            _db = db;
            _tokenSvc = tokenSvc;
            _config = config;
        }

        // ======================================================
        // LOGIN
        // ======================================================
        public async Task<LoginResponseDTO> LoginAsync(LoginRequestDTO dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();

            // Consulta de solo lectura, incluye la empresa
            var user = await _db.Usuarios
                .AsNoTracking()
                .Include(u => u.Empresa)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user is null || !PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash, user.PasswordSalt))
                throw new UnauthorizedAccessException("Credenciales inválidas");

            // Validaciones según rol
            if (user.Rol == RolUsuario.AdminEmpresa || user.Rol == RolUsuario.Usuario)
            {
                if (!user.EmpresaId.HasValue || user.Empresa is null)
                    throw new UnauthorizedAccessException("Usuario sin empresa asignada");

                if (user.Empresa.Estado != EstadoEmpresa.Approved)
                    throw new UnauthorizedAccessException("La empresa aún no está aprobada");

                if (!user.Empresa.IsActive)
                    throw new UnauthorizedAccessException("La empresa está inactiva");
            }

            // Claims
            var accessMinutes = int.Parse(_config["JwtSettings:AccessTokenExpirationMinutes"]!);
            var expiresAt = DateTime.UtcNow.AddMinutes(accessMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.Role, user.Rol.ToString()),
            };

            if (user.EmpresaId.HasValue)
                claims.Add(new("empresaId", user.EmpresaId.Value.ToString()));

            var accessToken = _tokenSvc.CreateAccessToken(claims, expiresAt);

            // Refresh token
            var refreshDays = int.Parse(_config["JwtSettings:RefreshTokenExpirationDays"]!);
            var (plainRt, hashRt) = _tokenSvc.CreateRefreshToken();

            var rt = new RefreshToken
            {
                UsuarioId = user.Id,
                TokenHash = hashRt,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
            };

            _db.RefreshTokens.Add(rt);
            await _db.SaveChangesAsync();

            return new LoginResponseDTO
            {
                Tokens = new TokenResponseDTO
                {
                    AccessToken = accessToken,
                    RefreshToken = plainRt,
                    ExpiresIn = accessMinutes * 60
                },
                Usuario = new
                {
                    id = user.Id,
                    email = user.Email,
                    nombreCompleto = user.NombreCompleto,
                    rol = user.Rol.ToString(),
                    empresaId = user.EmpresaId
                }
            };
        }

        // ======================================================
        // REFRESH TOKEN
        // ======================================================
        public async Task<TokenResponseDTO> RefreshAsync(RefreshTokenRequestDTO dto)
        {
            var hash = _tokenSvc.HashRefreshToken(dto.RefreshToken);
            var stored = await _db.RefreshTokens
                .Include(r => r.Usuario)
                .FirstOrDefaultAsync(r => r.TokenHash == hash);

            if (stored is null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token inválido");

            var user = stored.Usuario;
            var accessMinutes = int.Parse(_config["JwtSettings:AccessTokenExpirationMinutes"]!);
            var expiresAt = DateTime.UtcNow.AddMinutes(accessMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.Role, user.Rol.ToString()),
            };

            if (user.EmpresaId.HasValue)
                claims.Add(new("empresaId", user.EmpresaId.Value.ToString()));

            var accessToken = _tokenSvc.CreateAccessToken(claims, expiresAt);

            // Rotación de refresh token
            stored.IsRevoked = true;
            stored.RevokedAt = DateTime.UtcNow;
            stored.RevokeReason = "rotated";

            var refreshDays = int.Parse(_config["JwtSettings:RefreshTokenExpirationDays"]!);
            var (plainRt, hashRt) = _tokenSvc.CreateRefreshToken();

            _db.RefreshTokens.Add(new RefreshToken
            {
                UsuarioId = user.Id,
                TokenHash = hashRt,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
            });

            await _db.SaveChangesAsync();

            return new TokenResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = plainRt,
                ExpiresIn = accessMinutes * 60
            };
        }

        // ======================================================
        // LOGOUT
        // ======================================================
        public async Task LogoutAsync(string refreshToken)
        {
            var hash = _tokenSvc.HashRefreshToken(refreshToken);
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
            if (stored is null) return;

            stored.IsRevoked = true;
            stored.RevokedAt = DateTime.UtcNow;
            stored.RevokeReason = "logout";
            await _db.SaveChangesAsync();
        }

        // ======================================================
        // REGISTRO ADMIN EMPRESA
        // ======================================================
        public async Task<int> RegisterAdminEmpresaAsync(RegisterAdminEmpresaDTO dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();

            // Validar duplicado
            if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                throw new InvalidOperationException("El email ya está registrado");

            // 1) Crear empresa en Pending
            var empresa = new Empresa
            {
                Nombre = dto.NombreEmpresa,
                Direccion = dto.DireccionEmpresa,
                Telefono = dto.TelefonoEmpresa
            };
            _db.Empresas.Add(empresa);
            await _db.SaveChangesAsync();

            // 2) Crear usuario AdminEmpresa asociado
            PasswordHasher.CreatePasswordHash(dto.Password, out var hash, out var salt);

            var user = new Usuario
            {
                Email = normalizedEmail,
                NombreCompleto = dto.NombreCompleto,
                Telefono = dto.Telefono,
                PasswordHash = hash,
                PasswordSalt = salt,
                Rol = RolUsuario.AdminEmpresa,
                EmpresaId = empresa.Id
            };

            _db.Usuarios.Add(user);
            await _db.SaveChangesAsync();

            return empresa.Id;
        }

        // ======================================================
        // REGISTRO ADMIN GENERAL
        // ======================================================
        public async Task<int> RegisterAdminGeneralAsync(RegisterAdminGeneralDTO dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();

            // Validar duplicado
            if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                throw new InvalidOperationException("El email ya está registrado");

            PasswordHasher.CreatePasswordHash(dto.Password, out var hash, out var salt);

            var user = new Usuario
            {
                Email = normalizedEmail,
                NombreCompleto = dto.NombreCompleto,
                Telefono = dto.Telefono,
                PasswordHash = hash,
                PasswordSalt = salt,
                Rol = RolUsuario.AdminGeneral,
                EmpresaId = null
            };

            _db.Usuarios.Add(user);
            await _db.SaveChangesAsync();

            return user.Id;
        }
    }
}