namespace TaskControlBackend.DTOs.Auth;

public class LoginResponseDTO
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = "Login exitoso";
    public TokenResponseDTO Tokens { get; set; } = null!;
    public object Usuario { get; set; } = null!;
}