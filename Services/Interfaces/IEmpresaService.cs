using TaskControlBackend.Models.Enums;
using TaskControlBackend.Models;

namespace TaskControlBackend.Services.Interfaces;

public interface IEmpresaService
{   
    Task<Empresa?> GetByIdAsync(Guid id);
    Task<bool> EmpresaEstaAprobadaAsync(Guid empresaId);
    Task<Guid> CrearEmpresaPendingAsync(string nombre, string? dir, string? tel);
    Task AprobarAsync(Guid empresaId);
    Task RechazarAsync(Guid empresaId, string? motivo = null);
    //para eliminarlas
    Task HardDeleteAsync(Guid empresaId);
}