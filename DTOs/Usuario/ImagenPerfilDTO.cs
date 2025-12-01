using Microsoft.AspNetCore.Http;

namespace DTOs.Usuario
{
    public class ImagenPerfilDTO
    {
        public IFormFile Imagen { get; set; }
    }
}