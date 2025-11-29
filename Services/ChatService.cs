using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Data;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext _db;

    public ChatService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(Guid chatId, Guid messageId, DateTimeOffset readAt)?> MarcarMensajeComoLeidoAsync(Guid messageId, Guid userId)
    {
        var message = await _db.Messages
            .Include(m => m.Chat)
                .ThenInclude(c => c.Members)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message is null)
            throw new KeyNotFoundException("Mensaje no encontrado");

        // Verificar que el usuario es miembro del chat
        if (!message.Chat.Members.Any(m => m.UserId == userId))
            throw new UnauthorizedAccessException("No eres miembro de este chat");

        // No marcar como leído si el usuario es el remitente
        if (message.SenderId == userId)
            return null;

        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return (message.ChatId, message.Id, message.ReadAt.Value);
        }
        
        return null;
    }

    public async Task<List<Guid>> MarcarTodosChatComoLeidosAsync(Guid chatId, Guid userId)
    {
        // Verificar que el usuario es miembro del chat
        var isMember = await _db.ChatMembers
            .AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

        if (!isMember)
            throw new UnauthorizedAccessException("No eres miembro de este chat");

        // Marcar todos los mensajes no leídos que no fueron enviados por el usuario
        var mensajesNoLeidos = await _db.Messages
            .Where(m => m.ChatId == chatId &&
                        m.SenderId != userId &&
                        !m.IsRead)
            .ToListAsync();

        var markedIds = new List<Guid>();
        
        if (mensajesNoLeidos.Any())
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var mensaje in mensajesNoLeidos)
            {
                mensaje.IsRead = true;
                mensaje.ReadAt = now;
                markedIds.Add(mensaje.Id);
            }
            await _db.SaveChangesAsync();
        }
        
        return markedIds;
    }

    public async Task<int> GetTotalMensajesNoLeidosAsync(Guid userId)
    {
        // Obtener todos los chats del usuario
        var chatIds = await _db.ChatMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.ChatId)
            .ToListAsync();

        // Contar mensajes no leídos en esos chats (excluir los enviados por el usuario)
        return await _db.Messages
            .Where(m => chatIds.Contains(m.ChatId) &&
                        m.SenderId != userId &&
                        !m.IsRead)
            .CountAsync();
    }

    public async Task<Dictionary<Guid, int>> GetMensajesNoLeidosPorChatAsync(Guid userId)
    {
        // Obtener todos los chats del usuario
        var chatIds = await _db.ChatMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.ChatId)
            .ToListAsync();

        // Agrupar mensajes no leídos por chat
        var result = await _db.Messages
            .Where(m => chatIds.Contains(m.ChatId) &&
                        m.SenderId != userId &&
                        !m.IsRead)
            .GroupBy(m => m.ChatId)
            .Select(g => new { ChatId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ChatId, x => x.Count);

        return result;
    }
}
