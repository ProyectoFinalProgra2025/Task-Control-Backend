using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TaskControlBackend.Helpers;

namespace TaskControlBackend.Hubs;

/// <summary>
/// SignalR Hub para comunicación en tiempo real de chat
///
/// ESTRATEGIA DE NOTIFICACIÓN OPTIMIZADA:
/// - Notifica a USUARIOS directamente via Clients.Users(userIds) - esto envía a TODAS las conexiones de cada usuario
/// - Permite a usuarios recibir mensajes incluso si están viendo otro chat
/// - Soporta múltiples chats concurrentes sin problemas
///
/// CONEXIÓN:
/// - URL: wss://api.taskcontrol.work/chathub?access_token={jwt}
/// - Requiere JWT válido en query string
///
/// EVENTOS EMITIDOS (para escuchar en Flutter):
/// - "chat:message" - Nuevo mensaje en cualquier chat
/// - "chat:message_delivered" - Mensaje marcado como entregado
/// - "chat:message_read" - Mensaje marcado como leído
/// - "chat:typing" - Usuario está escribiendo
/// - "chat:conversation_updated" - Conversación actualizada (nombre, imagen, etc.)
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Se ejecuta cuando un usuario se conecta al hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);
        var connectionId = Context.ConnectionId;

        _logger.LogInformation("Usuario {UserId} conectado con ConnectionId {ConnectionId}", userId, connectionId);

        // Flutter: Automáticamente al conectarse, el usuario está listo para recibir notificaciones
        // No necesita unirse manualmente a grupos, usamos Clients.Users() directamente

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Se ejecuta cuando un usuario se desconecta
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);
        var connectionId = Context.ConnectionId;

        if (exception != null)
        {
            _logger.LogWarning(exception, "Usuario {UserId} desconectado con error (ConnectionId: {ConnectionId})", userId, connectionId);
        }
        else
        {
            _logger.LogInformation("Usuario {UserId} desconectado normalmente (ConnectionId: {ConnectionId})", userId, connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Usuario se une a una conversación específica (opcional, para eventos de grupo)
    /// Flutter puede llamar esto cuando el usuario abre un chat
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

        _logger.LogInformation("Usuario {UserId} se unió a conversación {ConversationId}", userId, conversationId);
    }

    /// <summary>
    /// Usuario sale de una conversación específica
    /// Flutter puede llamar esto cuando el usuario cierra un chat
    /// </summary>
    public async Task LeaveConversation(string conversationId)
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

        _logger.LogInformation("Usuario {UserId} salió de conversación {ConversationId}", userId, conversationId);
    }

    /// <summary>
    /// Usuario está escribiendo en un chat
    /// Flutter llama esto cuando el usuario empieza a escribir
    /// </summary>
    /// <param name="conversationId">ID de la conversación</param>
    /// <param name="recipientUserIds">IDs de los usuarios que deben ser notificados (string separado por comas)</param>
    public async Task SendTypingIndicator(string conversationId, string recipientUserIds)
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);
        var userName = Context.User!.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario";

        // Convertir string de IDs a lista
        var recipientIds = recipientUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

        // Notificar SOLO a los otros usuarios (no al que escribe)
        await Clients.Users(recipientIds).SendAsync("chat:typing", new
        {
            conversationId,
            userId,
            userName,
            isTyping = true,
            timestamp = DateTimeOffset.UtcNow
        });

        _logger.LogDebug("Usuario {UserId} está escribiendo en conversación {ConversationId}", userId, conversationId);
    }

    /// <summary>
    /// Usuario dejó de escribir
    /// Flutter llama esto cuando el usuario deja de escribir
    /// </summary>
    public async Task SendStoppedTypingIndicator(string conversationId, string recipientUserIds)
    {
        var userId = ClaimsHelpers.GetUserId(Context.User!);
        var userName = Context.User!.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario";

        var recipientIds = recipientUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

        await Clients.Users(recipientIds).SendAsync("chat:typing", new
        {
            conversationId,
            userId,
            userName,
            isTyping = false,
            timestamp = DateTimeOffset.UtcNow
        });

        _logger.LogDebug("Usuario {UserId} dejó de escribir en conversación {ConversationId}", userId, conversationId);
    }
}
