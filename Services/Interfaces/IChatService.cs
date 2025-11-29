namespace TaskControlBackend.Services.Interfaces;

public interface IChatService
{
    /// <summary>
    /// Marca un mensaje como leído
    /// </summary>
    /// <returns>Tuple con chatId, messageId y readAt si se marcó, null si ya estaba leído</returns>
    Task<(Guid chatId, Guid messageId, DateTimeOffset readAt)?> MarcarMensajeComoLeidoAsync(Guid messageId, Guid userId);

    /// <summary>
    /// Marca todos los mensajes de un chat como leídos para un usuario
    /// </summary>
    /// <returns>Lista de IDs de mensajes que fueron marcados como leídos</returns>
    Task<List<Guid>> MarcarTodosChatComoLeidosAsync(Guid chatId, Guid userId);

    /// <summary>
    /// Obtiene el número total de mensajes no leídos para un usuario
    /// </summary>
    Task<int> GetTotalMensajesNoLeidosAsync(Guid userId);

    /// <summary>
    /// Obtiene el número de mensajes no leídos por chat para un usuario
    /// </summary>
    Task<Dictionary<Guid, int>> GetMensajesNoLeidosPorChatAsync(Guid userId);
}
