using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskControlBackend.Hubs;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Controllers;

[Route("api/chats")]
[Authorize]
public class ChatController : BaseController
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatAppHub> _hubContext;

    public ChatController(IChatService chatService, IHubContext<ChatAppHub> hubContext)
    {
        _chatService = chatService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// PUT /api/chat/messages/{messageId}/mark-read
    /// Marca un mensaje específico como leído
    /// </summary>
    [HttpPut("messages/{messageId:guid}/mark-read")]
    public async Task<IActionResult> MarcarMensajeComoLeido(Guid messageId)
    {
        var result = await _chatService.MarcarMensajeComoLeidoAsync(messageId, GetUserId());
        
        // Notificar al remitente que su mensaje fue leído via SignalR
        if (result != null)
        {
            await _hubContext.Clients.Group(result.Value.chatId.ToString())
                .SendAsync("chat:message-read", new 
                { 
                    messageId = result.Value.messageId, 
                    chatId = result.Value.chatId,
                    readAt = result.Value.readAt,
                    readBy = GetUserId()
                });
        }
        
        return Success("Mensaje marcado como leído");
    }

    /// <summary>
    /// PUT /api/chat/{chatId}/mark-all-read
    /// Marca todos los mensajes de un chat como leídos
    /// </summary>
    [HttpPut("{chatId:guid}/mark-all-read")]
    public async Task<IActionResult> MarcarTodosComoLeidos(Guid chatId)
    {
        var markedIds = await _chatService.MarcarTodosChatComoLeidosAsync(chatId, GetUserId());
        
        // Notificar que los mensajes fueron leídos
        if (markedIds.Count > 0)
        {
            await _hubContext.Clients.Group(chatId.ToString())
                .SendAsync("chat:messages-read", new 
                { 
                    chatId,
                    messageIds = markedIds,
                    readAt = DateTimeOffset.UtcNow,
                    readBy = GetUserId()
                });
        }
        
        return Success("Todos los mensajes marcados como leídos");
    }

    /// <summary>
    /// GET /api/chat/unread-count
    /// Obtiene el número total de mensajes no leídos del usuario
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _chatService.GetTotalMensajesNoLeidosAsync(GetUserId());
        return SuccessData(new { unreadCount = count });
    }

    /// <summary>
    /// GET /api/chat/unread-by-chat
    /// Obtiene el número de mensajes no leídos por cada chat
    /// </summary>
    [HttpGet("unread-by-chat")]
    public async Task<IActionResult> GetUnreadByChat()
    {
        var unreadByChatId = await _chatService.GetMensajesNoLeidosPorChatAsync(GetUserId());
        return SuccessData(unreadByChatId);
    }
}
