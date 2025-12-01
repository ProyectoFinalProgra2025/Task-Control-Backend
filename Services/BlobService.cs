using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

public class BlobService
{
    private readonly string _connectionString;
    private readonly string _containerName;

    public BlobService(IConfiguration config)
    {
        _connectionString = config["AzureStorage:ConnectionString"]
            ?? throw new InvalidOperationException("AzureStorage:ConnectionString no está configurado.");

        _containerName = config["AzureStorage:ContainerName"]
            ?? throw new InvalidOperationException("AzureStorage:ContainerName no está configurado.");
    }

    /// <summary>
    /// Sube un archivo genérico al contenedor principal
    /// </summary>
    public async Task<string> UploadFileAsync(IFormFile file)
    {
        return await UploadFileAsync(file, null);
    }

    /// <summary>
    /// Sube un archivo a una carpeta específica dentro del contenedor
    /// </summary>
    /// <param name="file">Archivo a subir</param>
    /// <param name="folder">Carpeta dentro del contenedor (ej: "profile-photos", "evidencias", "documentos")</param>
    public async Task<string> UploadFileAsync(IFormFile file, string? folder)
    {
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        await container.SetAccessPolicyAsync(PublicAccessType.Blob);

        string blobName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        
        // Si hay folder, agregarlo al path
        if (!string.IsNullOrWhiteSpace(folder))
        {
            blobName = $"{folder.Trim('/')}/{blobName}";
        }
        
        var blob = container.GetBlobClient(blobName);

        using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });

        return blob.Uri.ToString();
    }

    /// <summary>
    /// Sube una foto de perfil de usuario
    /// </summary>
    public async Task<string> UploadProfilePhotoAsync(IFormFile file, Guid usuarioId)
    {
        ValidateImageFile(file);
        return await UploadFileAsync(file, $"profile-photos/{usuarioId}");
    }

    /// <summary>
    /// Sube un documento adjunto de tarea
    /// </summary>
    public async Task<string> UploadTareaDocumentoAsync(IFormFile file, Guid tareaId)
    {
        ValidateDocumentFile(file);
        return await UploadFileAsync(file, $"tareas/{tareaId}/documentos");
    }

    /// <summary>
    /// Sube una evidencia de tarea
    /// </summary>
    public async Task<string> UploadTareaEvidenciaAsync(IFormFile file, Guid tareaId)
    {
        ValidateDocumentFile(file);
        return await UploadFileAsync(file, $"tareas/{tareaId}/evidencias");
    }

    /// <summary>
    /// Sube un archivo CSV para importación masiva
    /// </summary>
    public async Task<string> UploadCsvAsync(IFormFile file, Guid empresaId)
    {
        ValidateCsvFile(file);
        return await UploadFileAsync(file, $"imports/{empresaId}");
    }

    /// <summary>
    /// Sube un archivo de chat (imágenes, documentos, audio, video)
    /// </summary>
    public async Task<string> UploadChatFileAsync(IFormFile file, Guid conversationId)
    {
        ValidateChatFile(file);
        return await UploadFileAsync(file, $"chat/{conversationId}");
    }

    /// <summary>
    /// Elimina un blob por su URL
    /// </summary>
    public async Task<bool> DeleteBlobAsync(string blobUrl)
    {
        try
        {
            var uri = new Uri(blobUrl);
            var blobName = string.Join("/", uri.Segments.Skip(2)); // Skip container name
            
            var container = new BlobContainerClient(_connectionString, _containerName);
            var blob = container.GetBlobClient(blobName);
            
            var response = await blob.DeleteIfExistsAsync();
            return response.Value;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Valida que el archivo sea una imagen válida
    /// </summary>
    private void ValidateImageFile(IFormFile file)
    {
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            throw new ArgumentException($"Tipo de archivo no permitido. Tipos permitidos: {string.Join(", ", allowedTypes)}");

        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
            throw new ArgumentException($"Extensión de archivo no permitida. Extensiones permitidas: {string.Join(", ", allowedExtensions)}");

        // Límite de 5MB para imágenes
        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("El archivo excede el tamaño máximo permitido de 5MB");
    }

    /// <summary>
    /// Valida que el archivo sea un documento válido (PDF, Excel, imágenes, etc.)
    /// </summary>
    private void ValidateDocumentFile(IFormFile file)
    {
        var allowedTypes = new[] 
        { 
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/pdf",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/csv",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

        var allowedExtensions = new[] 
        { 
            ".jpg", ".jpeg", ".png", ".gif", ".webp",
            ".pdf",
            ".xls", ".xlsx",
            ".csv",
            ".doc", ".docx"
        };

        if (!allowedTypes.Contains(file.ContentType.ToLower()))
        {
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException($"Tipo de archivo no permitido. Extensiones permitidas: {string.Join(", ", allowedExtensions)}");
        }

        // Límite de 25MB para documentos
        if (file.Length > 25 * 1024 * 1024)
            throw new ArgumentException("El archivo excede el tamaño máximo permitido de 25MB");
    }

    /// <summary>
    /// Valida que el archivo sea un CSV válido
    /// </summary>
    private void ValidateCsvFile(IFormFile file)
    {
        var allowedTypes = new[] { "text/csv", "application/vnd.ms-excel" };
        var allowedExtensions = new[] { ".csv" };

        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
            throw new ArgumentException("El archivo debe ser un CSV (.csv)");

        // Límite de 10MB para CSV
        if (file.Length > 10 * 1024 * 1024)
            throw new ArgumentException("El archivo CSV excede el tamaño máximo permitido de 10MB");
    }

    /// <summary>
    /// Valida que el archivo sea válido para chat (imágenes, documentos, audio, video)
    /// </summary>
    private void ValidateChatFile(IFormFile file)
    {
        var allowedExtensions = new[] 
        { 
            // Imágenes
            ".jpg", ".jpeg", ".png", ".gif", ".webp",
            // Documentos
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt",
            // Audio
            ".mp3", ".wav", ".ogg", ".m4a",
            // Video
            ".mp4", ".avi", ".mov", ".wmv", ".mkv"
        };

        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
            throw new ArgumentException($"Tipo de archivo no permitido para chat. Extensiones permitidas: imágenes, documentos, audio y video.");

        // Límite de 50MB para archivos de chat
        if (file.Length > 50 * 1024 * 1024)
            throw new ArgumentException("El archivo excede el tamaño máximo permitido de 50MB");
    }
}
