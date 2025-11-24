using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Data;
using TaskControlBackend.Models.Enums;

namespace TaskControlBackend.Hubs;

[Authorize]
public class ChatAppHub : Hub
{
    private readonly AppDbContext _db;
    
    public ChatAppHub(AppDbContext db)
    {
        _db = db;
    }

    // ==================== CHAT METHODS ====================
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

    // ==================== ENTERPRISE GROUPS ====================
    public async Task JoinEmpresaGroup(Guid empresaId)
    {
        var userId = GetUserId();
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == userId && u.EmpresaId == empresaId);
        if (usuario == null) throw new HubException("No perteneces a esta empresa");
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"empresa_{empresaId}");
    }

    public async Task LeaveEmpresaGroup(Guid empresaId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"empresa_{empresaId}");
    }

    // ==================== SUPER ADMIN GROUP ====================
    public async Task JoinSuperAdminGroup()
    {
        var userId = GetUserId();
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == userId);
        if (usuario == null || usuario.Rol != RolUsuario.AdminGeneral) 
            throw new HubException("No tienes permisos de AdminGeneral");
        
        await Groups.AddToGroupAsync(Context.ConnectionId, "super_admin");
    }

    public async Task LeaveSuperAdminGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "super_admin");
    }

    // ==================== DEPARTMENT GROUPS ====================
    public async Task JoinDepartmentGroup(Guid empresaId, string departamento)
    {
        var userId = GetUserId();
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == userId && u.EmpresaId == empresaId);
        if (usuario == null) throw new HubException("Usuario no encontrado");
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"empresa_{empresaId}_dept_{departamento}");
    }

    public async Task LeaveDepartmentGroup(Guid empresaId, string departamento)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"empresa_{empresaId}_dept_{departamento}");
    }

    private Guid GetUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                  Context.User?.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : throw new HubException("Usuario inválido");
    }
}
