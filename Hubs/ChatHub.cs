using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Collections.Concurrent;
using TaskControlBackend.Helpers;
using TaskControlBackend.Data;

namespace TaskControlBackend.Hubs;

/// <summary>
/// SignalR Hub para comunicación en tiempo real de chat
/// Optimizado y robusto con manejo de errores mejorado
///
/// CONEXIÓN:
/// - URL: wss://api.taskcontrol.work/chathub?access_token={jwt}
/// - Requiere JWT válido en query string
///
/// EVENTOS QUE EL CLIENTE PUEDE ESCUCHAR:
/// - "ReceiveMessage" -> Nuevo mensaje en cualquier chat
/// - "MessageDelivered" -> Confirmación de entrega (✓)
/// - "MessageRead" -> Confirmación de lectura (✓✓)
/// - "UserTyping" -> Usuario está escribiendo
///
/// MÉTODOS QUE EL CLIENTE PUEDE INVOCAR:
/// - JoinConversation(conversationId) -> Unirse a un chat específico
/// - LeaveConversation(conversationId) -> Salir de un chat específico
/// - SendTyping(conversationId) -> Indicar que está escribiendo
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
    /// Conectar usuario al hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = ClaimsHelpers.GetUserId(Context.User!);
            var connectionId = Context.ConnectionId;

            _logger.LogInformation("✅ ChatHub: Usuario {UserId} conectado (ConnectionId: {ConnectionId})",
                userId, connectionId);

            // Unir al grupo personal del usuario
            // Esto permite enviar notificaciones directamente a TODAS las conexiones del usuario
            await Groups.AddToGroupAsync(connectionId, $"user_{userId}");

            _logger.LogInformation("📱 Usuario {UserId} añadido a grupo personal 'user_{UserId}'", userId, userId);

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en OnConnectedAsync");
            Context.Abort();
        }
    }

    /// <summary>
    /// Desconectar usuario del hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = ClaimsHelpers.GetUserId(Context.User!);
            var connectionId = Context.ConnectionId;

            if (exception != null)
            {
                _logger.LogWarning(exception,
                    "❌ Usuario {UserId} desconectado con error (ConnectionId: {ConnectionId})",
                    userId, connectionId);
            }
            else
            {
                _logger.LogInformation(
                    "👋 Usuario {UserId} desconectado normalmente (ConnectionId: {ConnectionId})",
                    userId, connectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en OnDisconnectedAsync");
        }
    }

    /// <summary>
    /// Usuario se une a una conversación específica
    /// Flutter llama esto cuando el usuario abre un chat
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        try
        {
            var userId = ClaimsHelpers.GetUserId(Context.User!);

            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

            _logger.LogInformation(
                "💬 Usuario {UserId} se unió a conversación {ConversationId} (ConnectionId: {ConnectionId})",
                userId, conversationId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en JoinConversation: {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Usuario sale de una conversación específica
    /// Flutter llama esto cuando el usuario cierra un chat
    /// </summary>
    public async Task LeaveConversation(string conversationId)
    {
        try
        {
            var userId = ClaimsHelpers.GetUserId(Context.User!);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

            _logger.LogInformation(
                "🚪 Usuario {UserId} salió de conversación {ConversationId} (ConnectionId: {ConnectionId})",
                userId, conversationId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en LeaveConversation: {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Enviar indicador de que el usuario está escribiendo
    /// Flutter puede llamar esto cuando el usuario empieza a escribir
    /// </summary>
    public async Task SendTyping(string conversationId)
    {
        try
        {
            var userId = ClaimsHelpers.GetUserId(Context.User!);
            var userName = Context.User!.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario";

            // Notificar SOLO al grupo de la conversación (no al sender)
            await Clients.GroupExcept($"conversation_{conversationId}", Context.ConnectionId)
                .SendAsync("UserTyping", new
                {
                    conversationId,
                    senderId = userId,
                    senderName = userName,
                    isTyping = true,
                    timestamp = DateTimeOffset.UtcNow
                });

            _logger.LogDebug("⌨️ Usuario {UserId} está escribiendo en conversación {ConversationId}",
                userId, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en SendTyping: {ConversationId}", conversationId);
        }
    }
}
