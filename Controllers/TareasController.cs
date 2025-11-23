using System.Security.Claims; // manejo de claims
using Microsoft.AspNetCore.Authorization; // autorización
using Microsoft.AspNetCore.Mvc; // controladores MVC
using TaskControlBackend.DTOs.Tarea; // DTOs de tareas
using TaskControlBackend.Models.Enums; // enums de roles y estados
using TaskControlBackend.Services.Interfaces; // interfaz del servicio
using System.IdentityModel.Tokens.Jwt; // manejo del JWT

namespace TaskControlBackend.Controllers
{
    [ApiController] // controlador API
    [Route("api/[controller]")] // ruta base
    [Authorize] // requiere autenticación
    public class TareasController : ControllerBase
    {
        private readonly ITareaService _svc; // servicio de tareas

        public TareasController(ITareaService svc)
        {
            _svc = svc; // inyección del servicio
        }

        private RolUsuario Rol()
        {
            var r = User.FindFirstValue(ClaimTypes.Role); // obtiene rol del token
            return Enum.TryParse<RolUsuario>(r, out var rol) ? rol : RolUsuario.Usuario; // retorna rol
        }

        private Guid UserId() =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User.FindFirstValue(JwtRegisteredClaimNames.Sub)!); // obtiene userId del token

        private Guid? EmpresaIdClaim()
        {
            var v = User.FindFirst("empresaId")?.Value; // obtiene empresaId del token
            return Guid.TryParse(v, out var id) ? id : (Guid?)null; // retorna empresaId o null
        }

        // POST /api/tareas - crear tarea sin asignar, solo AdminEmpresa
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTareaDTO dto)
        {
            if (Rol() != RolUsuario.AdminEmpresa) // solo AdminEmpresa
                return Forbid();

            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            if (!ModelState.IsValid) // valida DTO
                return UnprocessableEntity(ModelState);

            var id = await _svc.CreateAsync(empresaId.Value, UserId(), dto); // crea tarea

            return StatusCode(201, new
            {
                success = true,
                message = "Tarea creada en estado PENDIENTE",
                data = new { id }
            });
        }

        // GET /api/tareas - listar tareas con filtros
        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] EstadoTarea? estado,
            [FromQuery] PrioridadTarea? prioridad,
            [FromQuery] Departamento? departamento,
            [FromQuery] Guid? asignadoA)
        {
            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            var list = await _svc.ListAsync(
                empresaId.Value, // empresa del usuario
                Rol(), // rol para lógica interna
                UserId(), // usuario
                estado, // filtros
                prioridad,
                departamento,
                asignadoA
            );

            return Ok(new { success = true, data = list }); // retorna listado
        }

        // GET /api/tareas/{id} - obtener tarea por id
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            var dto = await _svc.GetAsync(empresaId.Value, Rol(), UserId(), id); // obtiene tarea
            if (dto is null) return NotFound(); // si no existe

            return Ok(new { success = true, data = dto }); // retorna tarea
        }

        // PUT /api/tareas/{id}/asignar-manual - asignación manual por empresa
        [HttpPut("{id:guid}/asignar-manual")]
        public async Task<IActionResult> AsignarManual(Guid id, [FromBody] AsignarManualTareaDTO dto)
        {
            if (Rol() != RolUsuario.AdminEmpresa) // solo AdminEmpresa
                return Forbid();

            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            await _svc.AsignarManualAsync(empresaId.Value, id, dto); // asignación manual

            return Ok(new { success = true, message = "Tarea asignada manualmente" });
        }

        // PUT /api/tareas/{id}/asignar-automatico - asignación automática
        [HttpPut("{id:guid}/asignar-automatico")]
        public async Task<IActionResult> AsignarAutomatico(Guid id, [FromBody] AsignarAutomaticoTareaDTO dto)
        {
            if (Rol() != RolUsuario.AdminEmpresa) // solo AdminEmpresa
                return Forbid();

            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            await _svc.AsignarAutomaticamenteAsync(empresaId.Value, id, dto.ForzarReasignacion); // asignación automática

            return Ok(new { success = true, message = "Asignación automática ejecutada" });
        }

        // PUT /api/tareas/{id}/aceptar - usuario acepta su tarea
        [HttpPut("{id:guid}/aceptar")]
        public async Task<IActionResult> Aceptar(Guid id)
        {
            if (Rol() != RolUsuario.Usuario) // solo usuarios
                return Forbid();

            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            await _svc.AceptarAsync(empresaId.Value, id, UserId()); // acepta tarea

            return Ok(new { success = true, message = "Tarea aceptada" });
        }
        
        // GET /api/tareas/mis
// Solo para Usuario (trabajador) → devuelve las tareas asignadas a él mismo
        [HttpGet("mis")]
        public async Task<IActionResult> MisTareas(
            [FromQuery] EstadoTarea? estado,
            [FromQuery] PrioridadTarea? prioridad,
            [FromQuery] Departamento? departamento)
        {
            var rol = Rol();
            if (rol != RolUsuario.Usuario)
                return Forbid();

            var empresaId = EmpresaIdClaim();
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            var list = await _svc.ListAsync(
                empresaId.Value,
                rol,
                UserId(),
                estado,
                prioridad,
                departamento,
                asignadoAUsuarioId: null   // se ignora en el servicio cuando es Usuario
            );

            return Ok(new { success = true, data = list });
        }

        // PUT /api/tareas/{id}/finalizar - usuario finaliza tarea
        [HttpPut("{id:guid}/finalizar")]
        public async Task<IActionResult> Finalizar(Guid id, [FromBody] FinalizarTareaDTO dto)
        {
            if (Rol() != RolUsuario.Usuario) // solo usuarios
                return Forbid();

            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            if (!ModelState.IsValid) // valida DTO
                return UnprocessableEntity(ModelState);

            await _svc.FinalizarAsync(empresaId.Value, id, UserId(), dto); // finaliza tarea

            return Ok(new { success = true, message = "Tarea finalizada con evidencia" });
        }

        // PUT /api/tareas/{id}/cancelar - cancelar tarea
        [HttpPut("{id:guid}/cancelar")]
        public async Task<IActionResult> Cancelar(Guid id, [FromBody] string? motivo)
        {
            if (Rol() != RolUsuario.AdminEmpresa) // solo AdminEmpresa
                return Forbid();

            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            await _svc.CancelarAsync(empresaId.Value, id, UserId(), motivo); // cancela tarea

            return Ok(new { success = true, message = "Tarea cancelada" });
        }

        // PUT /api/tareas/{id}/reasignar - legado opcional
        public class ReasignarTareaDTO
        {
            public Guid? UsuarioId { get; set; } // usuario manual
            public bool AsignacionAutomatica { get; set; } = false; // modo automático
        }

        [HttpPut("{id:guid}/reasignar")]
        public async Task<IActionResult> Reasignar(Guid id, [FromBody] ReasignarTareaDTO dto)
        {
            if (Rol() != RolUsuario.AdminEmpresa) // solo AdminEmpresa
                return Forbid();

            var empresaId = EmpresaIdClaim(); // obtiene empresaId
            if (empresaId is null)
                return BadRequest(new { success = false, message = "EmpresaId no encontrado en el token" });

            // si es automático
            if (dto.AsignacionAutomatica)
            {
                await _svc.AsignarAutomaticamenteAsync(empresaId.Value, id, true); // reasignación automática
            }
            else
            {
                await _svc.AsignarManualAsync(empresaId.Value, id, new AsignarManualTareaDTO { UsuarioId = dto.UsuarioId }); // manual
            }

            return Ok(new { success = true, message = "Tarea reasignada" });
        }
    }
}
