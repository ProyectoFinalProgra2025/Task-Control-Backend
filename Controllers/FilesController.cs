using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly BlobService _blobService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(BlobService blobService, ILogger<FilesController> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    /// <summary>
    /// Sube un archivo genérico a Azure Blob Storage
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Archivo vacío" });

        try
        {
            _logger.LogInformation("Subiendo archivo: {FileName}, Tamaño: {Size} bytes", file.FileName, file.Length);
            
            var url = await _blobService.UploadFileAsync(file);

            _logger.LogInformation("Archivo subido exitosamente: {Url}", url);

            return Ok(new 
            { 
                success = true,
                data = new 
                { 
                    fileName = file.FileName, 
                    blobUrl = url,
                    contentType = file.ContentType,
                    size = file.Length
                }
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Error de validación al subir archivo: {Message}", ex.Message);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al subir archivo: {FileName}", file.FileName);
            return StatusCode(500, new { success = false, message = $"Error interno: {ex.Message}" });
        }
    }

    /// <summary>
    /// Sube un archivo a una carpeta específica
    /// </summary>
    [HttpPost("upload/{folder}")]
    public async Task<IActionResult> UploadToFolder(string folder, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Archivo vacío" });

        try
        {
            _logger.LogInformation("Subiendo archivo a carpeta {Folder}: {FileName}, Tamaño: {Size} bytes", folder, file.FileName, file.Length);
            
            var url = await _blobService.UploadFileAsync(file, folder);

            _logger.LogInformation("Archivo subido exitosamente a {Folder}: {Url}", folder, url);

            return Ok(new 
            { 
                success = true,
                data = new 
                { 
                    fileName = file.FileName, 
                    blobUrl = url,
                    folder = folder,
                    contentType = file.ContentType,
                    size = file.Length
                }
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Error de validación al subir archivo: {Message}", ex.Message);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al subir archivo a carpeta {Folder}: {FileName}", folder, file.FileName);
            return StatusCode(500, new { success = false, message = $"Error interno: {ex.Message}" });
        }
    }

    /// <summary>
    /// Elimina un archivo de Azure Blob Storage por su URL
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] string blobUrl)
    {
        if (string.IsNullOrWhiteSpace(blobUrl))
            return BadRequest(new { success = false, message = "URL del blob es requerida" });

        try
        {
            var deleted = await _blobService.DeleteBlobAsync(blobUrl);

            if (deleted)
                return Ok(new { success = true, message = "Archivo eliminado" });
            else
                return NotFound(new { success = false, message = "Archivo no encontrado o no se pudo eliminar" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar archivo: {BlobUrl}", blobUrl);
            return StatusCode(500, new { success = false, message = $"Error interno: {ex.Message}" });
        }
    }
}
