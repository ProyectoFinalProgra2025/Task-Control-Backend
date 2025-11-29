using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Auth;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private readonly IAuthService _auth;
        private readonly AppDbContext _db;

        public AuthController(IAuthService auth, AppDbContext db)
        {
            _auth = auth;
            _db = db;
        }
        
        // LOGIN
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

            var res = await _auth.LoginAsync(dto);
            return Ok(new { success = true, message = "Login exitoso", data = res });
        }
        
        // REFRESH TOKEN
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDTO dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

            var res = await _auth.RefreshAsync(dto);
            return Ok(new { success = true, data = res });
        }

        // LOGOUT
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDTO dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

            await _auth.LogoutAsync(dto.RefreshToken);
            return Ok(new { success = true, message = "Logout OK" });
        }

        // REGISTRO ADMIN EMPRESA
        [HttpPost("register-adminempresa")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterAdminEmpresa([FromBody] RegisterAdminEmpresaDTO dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

            var empresaId = await _auth.RegisterAdminEmpresaAsync(dto);
            return StatusCode(201, new
            {
                success = true,
                message = "Empresa registrada en Pending",
                data = new { empresaId }
            });
        }

        // REGISTRO ADMIN GENERAL (SUPERADMIN)
        [HttpPost("register-admingeneral")]
        [AllowAnonymous] // Controlamos manualmente adentro
        public async Task<IActionResult> RegisterAdminGeneral([FromBody] RegisterAdminGeneralDTO dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

            // ¿Ya existe algún AdminGeneral?
            var existe = await _db.Usuarios.AnyAsync(u => u.Rol == RolUsuario.AdminGeneral);

            if (existe)
            {
                // Debe estar autenticado y tener rol AdminGeneral
                if (!User.Identity?.IsAuthenticated ?? true)
                    return Unauthorized(new { success = false, message = "Debe estar autenticado para crear otro AdminGeneral" });

                if (!IsAdminGeneral())
                    return Forbid();
            }

            var id = await _auth.RegisterAdminGeneralAsync(dto);
            return StatusCode(201, new
            {
                success = true,
                message = "AdminGeneral creado correctamente",
                data = new { id }
            });
        }
    }
}