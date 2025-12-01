namespace TaskControlBackend.Models;

/// <summary>
/// Evidencias que el worker/manager sube al trabajar o finalizar una tarea.
/// Puede ser texto, archivos (PDF, Excel, imágenes), o ambos.
/// </summary>
public class TareaEvidencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TareaId { get; set; }
    public Tarea Tarea { get; set; } = null!;
    
    /// <summary>
    /// Texto descriptivo de la evidencia (opcional si hay archivo)
    /// </summary>
    public string? Descripcion { get; set; }
    
    /// <summary>
    /// Nombre original del archivo de evidencia (null si es solo texto)
    /// </summary>
    public string? NombreArchivo { get; set; }
    
    /// <summary>
    /// URL del archivo en Azure Blob Storage (null si es solo texto)
    /// </summary>
    public string? ArchivoUrl { get; set; }
    
    /// <summary>
    /// Tipo MIME del archivo (null si es solo texto)
    /// </summary>
    public string? TipoMime { get; set; }
    
    /// <summary>
    /// Tamaño del archivo en bytes (0 si es solo texto)
    /// </summary>
    public long TamanoBytes { get; set; }
    
    /// <summary>
    /// Usuario que subió la evidencia
    /// </summary>
    public Guid SubidoPorUsuarioId { get; set; }
    public Usuario SubidoPorUsuario { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
