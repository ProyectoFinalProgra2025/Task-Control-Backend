namespace TaskControlBackend.DTOs.Empresa
{
    public class EmpresaEstadisticasDTO
    {
        public Guid EmpresaId { get; set; }
        public string NombreEmpresa { get; set; } = null!;

        public int TotalTrabajadores { get; set; }
        public int TrabajadoresActivos { get; set; }

        // Campos para tareas (por ahora 0 hasta que tengamos el modelo Tarea)
        public int TotalTareas { get; set; }
        public int TareasPendientes { get; set; }
        public int TareasAsignadas { get; set; }
        public int TareasAceptadas { get; set; }
        public int TareasFinalizadas { get; set; }
        public int TareasCanceladas { get; set; }
    }
}