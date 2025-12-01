using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace DTOs.Tarea
{
    public class TareaEvidenciaDTO
    {
        public string Texto { get; set; } // Evidencia obligatoria
        public List<IFormFile> Archivos { get; set; } // Imágenes o documentos opcionales
    }
}