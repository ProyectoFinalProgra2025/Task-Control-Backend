namespace TaskControlBackend.DTOs.Empresa;

public class EmpresaListDTO
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}