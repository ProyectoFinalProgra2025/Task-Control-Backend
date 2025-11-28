namespace TaskControlBackend.Services.Interfaces;

public interface IChatService
{
    /// <summary>
    /// Marca un mensaje como leído
    /// </summary>
    Task MarcarMensajeComoLeidoAsync(Guid messageId, Guid userId);

    /// <summary>
    /// Marca todos los mensajes de un chat como leídos para un usuario
    /// </summary>
    Task MarcarTodosChatComoLeidosAsync(Guid chatId, Guid userId);

    /// <summary>
    /// Obtiene el número total de mensajes no leídos para un usuario
    /// </summary>
    Task<int> GetTotalMensajesNoLeidosAsync(Guid userId);

    /// <summary>
    /// Obtiene el número de mensajes no leídos por chat para un usuario
    /// </summary>
    Task<Dictionary<Guid, int>> GetMensajesNoLeidosPorChatAsync(Guid userId);
}
