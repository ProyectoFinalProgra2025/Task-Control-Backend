using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Data;

namespace TaskControlBackend.Hubs;

[Authorize]
public class ChatAppHub : Hub
{
    private readonly AppDbContext _db;
    
    public ChatAppHub(AppDbContext db)
    {
        _db = db;
    }

    public async Task JoinChat(Guid chatId)
    {
        var userId = GetUserId();
        var isMember = await _db.ChatMembers.AnyAsync(m => m.ChatId == chatId && m.UserId == userId);
        if (!isMember) throw new HubException("No eres miembro de este chat");
        await Groups.AddToGroupAsync(Context.ConnectionId, chatId.ToString());
    }

    public async Task LeaveChat(Guid chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId.ToString());
    }

    private int GetUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                  Context.User?.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : throw new HubException("Usuario inválido");
    }
}
