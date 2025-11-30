namespace TaskControlBackend.DTOs.Empresa
{
    public class TrabajadorColaDTO
    {
        public Guid Id { get; set; }
        public string NombreCompleto { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Departamento { get; set; }
        public int? NivelHabilidad { get; set; }
        public int TareasActivas { get; set; }
        public int LimiteMaximo { get; set; }
        public bool Disponible { get; set; }
    }
}
