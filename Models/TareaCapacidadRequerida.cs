namespace TaskControlBackend.Models
{
    public class TareaCapacidadRequerida
    {
        public int Id { get; set; }
        public int TareaId { get; set; }
        public Tarea Tarea { get; set; } = null!;
        public string Nombre { get; set; } = null!; // Ej: "Buen diseñador"
    }
}