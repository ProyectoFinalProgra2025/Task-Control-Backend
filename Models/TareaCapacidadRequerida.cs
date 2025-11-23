namespace TaskControlBackend.Models
{
    public class TareaCapacidadRequerida
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TareaId { get; set; }
        public Tarea Tarea { get; set; } = null!;
        public string Nombre { get; set; } = null!; // Ej: "Buen diseñador"
    }
}