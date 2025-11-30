using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskControlBackend.Helpers;

namespace TaskControlBackend.Hubs;

/// <summary>
/// SignalR Hub para actualizaciones en tiempo real de TAREAS y MÉTRICAS
/// 
/// PROPÓSITO:
/// - Notificar cambios en tareas (creación, asignación, aceptación, finalización)
/// - Actualizar métricas en dashboards
/// - Notificar cambios en usuarios/equipo
/// - Cualquier actualización que NO sea chat
/// 
/// ESTRATEGIA:
/// - Usuarios se unen automáticamente al grupo de su empresa
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
    /// Automáticamente lo une al grupo de su empresa
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);
        var empresaId = ClaimsHelpers.GetEmpresaId(Context.User!);
        var connectionId = Context.ConnectionId;

        _logger.LogInformation(
            "TareaHub: Usuario {UserId} conectado (Empresa: {EmpresaId}, ConnectionId: {ConnectionId})", 
            userId, empresaId, connectionId);

        // Unir al grupo de su empresa para recibir eventos
        if (empresaId.HasValue)
        {
            await Groups.AddToGroupAsync(connectionId, $"empresa_{empresaId.Value}");
            _logger.LogInformation("TareaHub: Usuario {UserId} unido al grupo empresa_{EmpresaId}", userId, empresaId.Value);
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
