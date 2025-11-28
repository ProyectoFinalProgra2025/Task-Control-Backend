using System.Security.Claims; // manejo de claims
using Microsoft.AspNetCore.Authorization; // autorización
using Microsoft.AspNetCore.Mvc; // controladores MVC
using TaskControlBackend.DTOs.Tarea; // DTOs de tareas
using TaskControlBackend.Models.Enums; // enums de roles y estados
using TaskControlBackend.Services.Interfaces; // interfaz del servicio
using System.IdentityModel.Tokens.Jwt; // manejo del JWT

namespace TaskControlBackend.Controllers
{
    [Route("api/[controller]")]
    public class TareasController : BaseController
    {
        private readonly ITareaService _svc;

        public TareasController(ITareaService svc)
        {
            _svc = svc;
        }

        // POST /api/tareas - crear tarea sin asignar (AdminEmpresa o ManagerDepartamento)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTareaDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.AdminEmpresa && rol != RolUsuario.ManagerDepartamento)
                return Forbid();

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            var id = await _svc.CreateAsync(GetEmpresaIdOrThrow(), GetUserId(), dto);

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
            var list = await _svc.ListAsync(
                GetEmpresaIdOrThrow(),
                GetRole(),
                GetUserId(),
                estado,
                prioridad,
                departamento,
                asignadoA
            );

            return SuccessData(list);
        }

        // GET /api/tareas/{id} - obtener tarea por id
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var dto = await _svc.GetAsync(GetEmpresaIdOrThrow(), GetRole(), GetUserId(), id); // obtiene tarea
            if (dto is null) return NotFound(); // si no existe

            return Ok(new { success = true, data = dto }); // retorna tarea
        }

        // PUT /api/tareas/{id}/asignar-manual - asignación manual (AdminEmpresa o ManagerDepartamento)
        [HttpPut("{id:guid}/asignar-manual")]
        public async Task<IActionResult> AsignarManual(Guid id, [FromBody] AsignarManualTareaDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.AdminEmpresa && rol != RolUsuario.ManagerDepartamento)
                return Forbid();

            await _svc.AsignarManualAsync(GetEmpresaIdOrThrow(), id, GetUserId(), dto);

            return Ok(new { success = true, message = "Tarea asignada manualmente" });
        }

        // PUT /api/tareas/{id}/asignar-automatico - asignación automática (AdminEmpresa o ManagerDepartamento)
        [HttpPut("{id:guid}/asignar-automatico")]
        public async Task<IActionResult> AsignarAutomatico(Guid id, [FromBody] AsignarAutomaticoTareaDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.AdminEmpresa && rol != RolUsuario.ManagerDepartamento)
                return Forbid();


            await _svc.AsignarAutomaticamenteAsync(GetEmpresaIdOrThrow(), id, dto.ForzarReasignacion); // asignación automática

            return Ok(new { success = true, message = "Asignación automática ejecutada" });
        }

        // PUT /api/tareas/{id}/aceptar - usuario o manager acepta su tarea
        [HttpPut("{id:guid}/aceptar")]
        public async Task<IActionResult> Aceptar(Guid id)
        {
            var rol = GetRole();
            if (rol != RolUsuario.Usuario && rol != RolUsuario.ManagerDepartamento) // usuarios y managers
                return Forbid();


            try
            {
                await _svc.AceptarAsync(GetEmpresaIdOrThrow(), id, GetUserId()); // acepta tarea
                return Ok(new { success = true, message = "Tarea aceptada" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        
        // GET /api/tareas/mis
// Para Usuario (trabajador) y ManagerDepartamento → devuelve las tareas asignadas a él mismo
        [HttpGet("mis")]
        public async Task<IActionResult> MisTareas(
            [FromQuery] EstadoTarea? estado,
            [FromQuery] PrioridadTarea? prioridad,
            [FromQuery] Departamento? departamento)
        {
            var rol = GetRole();
            if (rol != RolUsuario.Usuario && rol != RolUsuario.ManagerDepartamento)
                return Forbid();

            var list = await _svc.ListAsync(
                GetEmpresaIdOrThrow(),
                rol,
                GetUserId(),
                estado,
                prioridad,
                departamento,
                asignadoAUsuarioId: null   // se ignora en el servicio cuando es Usuario
            );

            return Ok(new { success = true, data = list });
        }

        // PUT /api/tareas/{id}/finalizar - usuario o manager finaliza tarea
        [HttpPut("{id:guid}/finalizar")]
        public async Task<IActionResult> Finalizar(Guid id, [FromBody] FinalizarTareaDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.Usuario && rol != RolUsuario.ManagerDepartamento) // usuarios y managers
                return Forbid();


            if (!ModelState.IsValid) // valida DTO
                return UnprocessableEntity(ModelState);

            await _svc.FinalizarAsync(GetEmpresaIdOrThrow(), id, GetUserId(), dto); // finaliza tarea

            return Ok(new { success = true, message = "Tarea finalizada con evidencia" });
        }

        // PUT /api/tareas/{id}/cancelar - cancelar tarea (AdminEmpresa o ManagerDepartamento)
        [HttpPut("{id:guid}/cancelar")]
        public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarTareaDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.AdminEmpresa && rol != RolUsuario.ManagerDepartamento)
                return Forbid();

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            await _svc.CancelarAsync(GetEmpresaIdOrThrow(), id, GetUserId(), dto.Motivo);

            return Ok(new { success = true, message = "Tarea cancelada" });
        }

        // PUT /api/tareas/{id}/reasignar - Reasignar tarea a otro usuario (diferente de asignación inicial)
        [HttpPut("{id:guid}/reasignar")]
        public async Task<IActionResult> Reasignar(Guid id, [FromBody] ReasignarTareaDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.AdminEmpresa && rol != RolUsuario.ManagerDepartamento)
                return Forbid();

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            await _svc.ReasignarAsync(
                GetEmpresaIdOrThrow(),
                id,
                GetUserId(), // quien reasigna
                dto.NuevoUsuarioId,
                dto.AsignacionAutomatica,
                dto.Motivo
            );

            return Success("Tarea reasignada correctamente");
        }

        // GET /api/tareas/{id}/historial-asignaciones - Ver historial de asignaciones de una tarea
        [HttpGet("{id:guid}/historial-asignaciones")]
        public async Task<IActionResult> GetHistorialAsignaciones(Guid id)
        {
            var historial = await _svc.GetHistorialAsignacionesAsync(id);
            return SuccessData(historial);
        }

        // ============================================================
        // DELEGACIÓN ENTRE JEFES DE ÁREA
        // ============================================================

        // PUT /api/tareas/{id}/delegar - Delegar tarea a otro jefe
        [HttpPut("{id:guid}/delegar")]
        public async Task<IActionResult> DelegarAJefe(Guid id, [FromBody] DelegarTareaDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.AdminEmpresa && rol != RolUsuario.ManagerDepartamento)
                return Forbid();

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            await _svc.DelegarTareaAJefeAsync(GetEmpresaIdOrThrow(), id, GetUserId(), dto);

            return Ok(new 
            { 
                success = true, 
                message = "Tarea delegada exitosamente. Esperando respuesta del jefe destino." 
            });
        }

        // PUT /api/tareas/{id}/aceptar-delegacion - Aceptar tarea delegada
        [HttpPut("{id:guid}/aceptar-delegacion")]
        public async Task<IActionResult> AceptarDelegacion(Guid id, [FromBody] AceptarDelegacionDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.ManagerDepartamento)
                return Forbid();

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            await _svc.AceptarDelegacionAsync(GetEmpresaIdOrThrow(), id, GetUserId(), dto);

            return Ok(new 
            { 
                success = true, 
                message = "Delegación aceptada. Ahora puedes gestionar esta tarea." 
            });
        }

        // PUT /api/tareas/{id}/rechazar-delegacion - Rechazar tarea delegada
        [HttpPut("{id:guid}/rechazar-delegacion")]
        public async Task<IActionResult> RechazarDelegacion(Guid id, [FromBody] RechazarDelegacionDTO dto)
        {
            var rol = GetRole();
            if (rol != RolUsuario.ManagerDepartamento)
                return Forbid();

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            await _svc.RechazarDelegacionAsync(GetEmpresaIdOrThrow(), id, GetUserId(), dto);

            return Ok(new 
            { 
                success = true, 
                message = "Delegación rechazada. La tarea regresa al jefe de origen." 
            });
        }
    }
}
