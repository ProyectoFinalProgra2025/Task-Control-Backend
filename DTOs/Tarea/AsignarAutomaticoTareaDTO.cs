// DTOs/Tarea/AsignarAutomaticoTareaDTO.cs
namespace TaskControlBackend.DTOs.Tarea
{
    // Por ahora es mínimo, pero lo dejo para futuro (ej: solo dentro de X departamento, etc.)
    public class AsignarAutomaticoTareaDTO
    {
        public bool ForzarReasignacion { get; set; } = false;
    }
}