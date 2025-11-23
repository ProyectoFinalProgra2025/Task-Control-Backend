// DTOs/Tarea/AsignarManualTareaDTO.cs
namespace TaskControlBackend.DTOs.Tarea
{
    public class AsignarManualTareaDTO
    {
        public Guid? UsuarioId { get; set; }          // opción A: por Id
        public string? NombreUsuario { get; set; }   // opción B: por nombre

        public bool IgnorarValidacionesSkills { get; set; } = false; // por si quieres permitir override en un futuro
    }
}