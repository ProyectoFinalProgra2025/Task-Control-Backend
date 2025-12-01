using TaskControlBackend.Models.Enums;
using TaskControlBackend.Models;
using TaskControlBackend.DTOs.Empresa;

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
    
    // Obtener información de cola de trabajadores
    Task<List<TrabajadorColaDTO>> GetTrabajadoresConColaAsync(Guid empresaId, Departamento? departamento = null);
}