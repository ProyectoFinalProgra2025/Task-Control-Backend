using System.ComponentModel.DataAnnotations;

namespace TaskControlBackend.DTOs.Auth;

public class RefreshTokenRequestDTO
{
    [Required] public string RefreshToken { get; set; } = null!;
}