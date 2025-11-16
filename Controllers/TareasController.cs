using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskControlBackend.DTOs.Tarea;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace TaskControlBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TareasController : ControllerBase
    {
        private readonly ITareaService _svc;

        public TareasController(ITareaService svc)
        {
            _svc = svc;
        }

        private RolUsuario Rol()
        {
            var r = User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<RolUsuario>(r, out var rol) ? rol : RolUsuario.Usuario;
        }

        private int UserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        private int? EmpresaIdClaim()
        {
            var v = User.FindFirst("empresaId")?.Value;
            return int.TryParse(v, out var id) ? id : (int?)null;
        }

        // POST /api/tareas
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTareaDTO dto)
        {
            var rol = Rol();
            if (rol != RolUsuario.AdminEmpresa && rol != RolUsuario.AdminGeneral)
                return Forbid();

            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            var id = await _svc.CreateAsync(empresaId.Value, UserId(), dto);
            return StatusCode(201, new { success = true, message = "Tarea creada", data = new { id } });
        }

        // GET /api/tareas?estado=&prioridad=&departamento=&asignadoA=
        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] EstadoTarea? estado,
            [FromQuery] PrioridadTarea? prioridad,
            [FromQuery] Departamento? departamento,
            [FromQuery] int? asignadoA)
        {
            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            var list = await _svc.ListAsync(
                empresaId.Value,
                Rol(),
                UserId(),
                estado,
                prioridad,
                departamento,
                asignadoA);

            return Ok(new { success = true, data = list });
        }

        // GET /api/tareas/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            var dto = await _svc.GetAsync(empresaId.Value, Rol(), UserId(), id);
            if (dto is null) return NotFound();

            return Ok(new { success = true, data = dto });
        }

        // PUT /api/tareas/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateTareaDTO dto)
        {
            if (Rol() != RolUsuario.AdminEmpresa && Rol() != RolUsuario.AdminGeneral)
                return Forbid();

            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            await _svc.UpdateAsync(empresaId.Value, id, dto);
            return Ok(new { success = true, message = "Tarea actualizada" });
        }

        // PUT /api/tareas/{id}/aceptar
        [HttpPut("{id:int}/aceptar")]
        public async Task<IActionResult> Aceptar([FromRoute] int id)
        {
            if (Rol() != RolUsuario.Usuario)
                return Forbid();

            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            await _svc.AceptarAsync(empresaId.Value, id, UserId());
            return Ok(new { success = true, message = "Tarea aceptada" });
        }

        // PUT /api/tareas/{id}/finalizar
        [HttpPut("{id:int}/finalizar")]
        public async Task<IActionResult> Finalizar([FromRoute] int id, [FromBody] FinalizarTareaDTO dto)
        {
            if (Rol() != RolUsuario.Usuario)
                return Forbid();

            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            await _svc.FinalizarAsync(empresaId.Value, id, UserId(), dto);
            return Ok(new { success = true, message = "Tarea finalizada" });
        }

        // PUT /api/tareas/{id}/cancelar
        [HttpPut("{id:int}/cancelar")]
        public async Task<IActionResult> Cancelar([FromRoute] int id, [FromBody] string? motivo)
        {
            if (Rol() != RolUsuario.AdminEmpresa && Rol() != RolUsuario.AdminGeneral)
                return Forbid();

            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            await _svc.CancelarAsync(empresaId.Value, id, UserId(), motivo);
            return Ok(new { success = true, message = "Tarea cancelada" });
        }

        // PUT /api/tareas/{id}/reasignar
        public class ReasignarTareaDTO
        {
            public int? UsuarioId { get; set; }
            public bool AsignacionAutomatica { get; set; } = false;
        }

        [HttpPut("{id:int}/reasignar")]
        public async Task<IActionResult> Reasignar([FromRoute] int id, [FromBody] ReasignarTareaDTO dto)
        {
            if (Rol() != RolUsuario.AdminEmpresa && Rol() != RolUsuario.AdminGeneral)
                return Forbid();

            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            await _svc.ReasignarAsync(empresaId.Value, id, UserId(), dto.UsuarioId, dto.AsignacionAutomatica);
            return Ok(new { success = true, message = "Tarea reasignada" });
        }
    }
}
