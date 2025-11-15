using TaskControlBackend.Models.Enums;
using TaskControlBackend.Models;

namespace TaskControlBackend.Services.Interfaces;

public interface IEmpresaService
{   
    Task<Empresa?> GetByIdAsync(int id);
    Task<bool> EmpresaEstaAprobadaAsync(int empresaId);
    Task<int> CrearEmpresaPendingAsync(string nombre, string? dir, string? tel);
    Task AprobarAsync(int empresaId);
    Task RechazarAsync(int empresaId, string? motivo = null);
    //para eliminarlas
    Task HardDeleteAsync(int empresaId);
}