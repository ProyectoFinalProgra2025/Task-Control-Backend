using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Controllers;

[Route("api/[controller]")]
[Authorize]
public class ChatController : BaseController
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// PUT /api/chat/messages/{messageId}/mark-read
    /// Marca un mensaje específico como leído
    /// </summary>
    [HttpPut("messages/{messageId:guid}/mark-read")]
    public async Task<IActionResult> MarcarMensajeComoLeido(Guid messageId)
    {
        await _chatService.MarcarMensajeComoLeidoAsync(messageId, GetUserId());
        return Success("Mensaje marcado como leído");
    }

    /// <summary>
    /// PUT /api/chat/{chatId}/mark-all-read
    /// Marca todos los mensajes de un chat como leídos
    /// </summary>
    [HttpPut("{chatId:guid}/mark-all-read")]
    public async Task<IActionResult> MarcarTodosComoLeidos(Guid chatId)
    {
        await _chatService.MarcarTodosChatComoLeidosAsync(chatId, GetUserId());
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
