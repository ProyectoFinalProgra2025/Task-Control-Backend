namespace TaskControlBackend.DTOs.Usuario
{
    public class ChangePasswordAdminGeneralDTO
    {
        public Guid UsuarioId { get; set; }
        public string NuevaPassword { get; set; } = null!;
    }
}