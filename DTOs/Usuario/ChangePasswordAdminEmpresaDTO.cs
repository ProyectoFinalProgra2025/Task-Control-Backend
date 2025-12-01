namespace TaskControlBackend.DTOs.Usuario
{
    public class ChangePasswordAdminEmpresaDTO
    {
        public Guid UsuarioId { get; set; }
        public string NuevaPassword { get; set; } = null!;
    }
}