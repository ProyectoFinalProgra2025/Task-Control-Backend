namespace TaskControlBackend.Models;

/// <summary>
/// Documentos adjuntos que se suben al CREAR una tarea.
/// Pueden ser PDFs, Excel, imágenes, etc. que el manager adjunta como referencia.
/// </summary>
public class TareaDocumentoAdjunto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TareaId { get; set; }
    public Tarea Tarea { get; set; } = null!;
    
    /// <summary>
    /// Nombre original del archivo subido
    /// </summary>
    public string NombreArchivo { get; set; } = null!;
    
    /// <summary>
    /// URL del archivo en Azure Blob Storage
    /// </summary>
    public string ArchivoUrl { get; set; } = null!;
    
    /// <summary>
    /// Tipo MIME del archivo (application/pdf, image/png, etc.)
    /// </summary>
    public string TipoMime { get; set; } = null!;
    
    /// <summary>
    /// Tamaño del archivo en bytes
    /// </summary>
    public long TamanoBytes { get; set; }
    
    /// <summary>
    /// Usuario que subió el documento
    /// </summary>
    public Guid SubidoPorUsuarioId { get; set; }
    public Usuario SubidoPorUsuario { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
