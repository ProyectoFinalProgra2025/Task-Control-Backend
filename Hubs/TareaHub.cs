using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskControlBackend.Helpers;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Hubs;

/// <summary>
/// SignalR Hub para actualizaciones en tiempo real de TAREAS, MÉTRICAS y EMPRESAS
/// 
/// PROPÓSITO:
/// - Notificar cambios en tareas (creación, asignación, aceptación, finalización)
/// - Actualizar métricas en dashboards
/// - Notificar cambios en usuarios/equipo
/// - Notificar cambios en empresas (solicitudes, aprobaciones, rechazos)
/// - Cualquier actualización que NO sea chat
/// 
/// ESTRATEGIA:
/// - Usuarios se unen automáticamente al grupo de su empresa
/// - SuperAdmin se une al grupo "super_admin" para recibir solicitudes de empresas
/// - Los eventos se emiten al grupo de empresa correspondiente
/// - Soporta múltiples conexiones del mismo usuario
///
/// CONEXIÓN:
/// - URL: /tareahub?access_token={jwt}
/// - Requiere JWT válido
///
/// EVENTOS EMITIDOS (para escuchar en Flutter):
/// - "tarea:created" - Nueva tarea creada
/// - "tarea:assigned" - Tarea asignada a un usuario
/// - "tarea:accepted" - Tarea aceptada por trabajador
/// - "tarea:completed" - Tarea finalizada
/// - "tarea:reasignada" - Tarea reasignada a otro usuario
/// - "tarea:updated" - Tarea editada (título, descripción, etc.)
/// - "tarea:deleted" - Tarea eliminada
/// - "metrics:updated" - Métricas actualizadas
/// - "user:updated" - Usuario actualizado
/// - "team:updated" - Equipo actualizado
/// - "empresa:created" - Nueva solicitud de empresa
/// - "empresa:approved" - Empresa aprobada
/// - "empresa:rejected" - Empresa rechazada
/// - "empresa:updated" - Empresa actualizada
/// </summary>
[Authorize]
public class TareaHub : Hub
{
    private readonly ILogger<TareaHub> _logger;

    public TareaHub(ILogger<TareaHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Se ejecuta cuando un usuario se conecta al hub
    /// Automáticamente lo une al grupo de su empresa y al grupo super_admin si es SuperAdmin
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);
        var empresaId = ClaimsHelpers.GetEmpresaId(Context.User!);
        var rol = ClaimsHelpers.GetRol(Context.User!);
        var connectionId = Context.ConnectionId;

        _logger.LogInformation(
            "TareaHub: Usuario {UserId} conectado (Empresa: {EmpresaId}, Rol: {Rol}, ConnectionId: {ConnectionId})", 
            userId, empresaId, rol, connectionId);

        // Unir al grupo de su empresa para recibir eventos
        if (empresaId.HasValue)
        {
            await Groups.AddToGroupAsync(connectionId, $"empresa_{empresaId.Value}");
            _logger.LogInformation("TareaHub: Usuario {UserId} unido al grupo empresa_{EmpresaId}", userId, empresaId.Value);
        }

        // Si es SuperAdmin, unir al grupo super_admin para recibir solicitudes de empresas
        if (rol == RolUsuario.AdminGeneral)
        {
            await Groups.AddToGroupAsync(connectionId, "super_admin");
            _logger.LogInformation("TareaHub: SuperAdmin {UserId} unido al grupo super_admin", userId);
        }

        // También unir a grupo personal para notificaciones directas
        await Groups.AddToGroupAsync(connectionId, $"user_{userId}");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Se ejecuta cuando un usuario se desconecta
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);
        var empresaId = ClaimsHelpers.GetEmpresaId(Context.User!);
        var connectionId = Context.ConnectionId;

        if (exception != null)
        {
            _logger.LogWarning(exception, 
                "TareaHub: Usuario {UserId} desconectado con error (ConnectionId: {ConnectionId})", 
                userId, connectionId);
        }
        else
        {
            _logger.LogInformation(
                "TareaHub: Usuario {UserId} desconectado normalmente (ConnectionId: {ConnectionId})", 
                userId, connectionId);
        }

        // Los grupos se limpian automáticamente al desconectar

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Método para que el cliente confirme que está listo para recibir eventos
    /// Útil para debugging y confirmar conexión
    /// </summary>
    public async Task Ping()
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);
        await Clients.Caller.SendAsync("pong", new { 
            userId, 
            timestamp = DateTimeOffset.UtcNow,
            message = "Connection alive"
        });
    }
}
