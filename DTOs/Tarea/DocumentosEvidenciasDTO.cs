namespace TaskControlBackend.DTOs.Tarea;

/// <summary>
/// DTO para representar un documento adjunto en la tarea
/// </summary>
public class DocumentoAdjuntoDTO
{
    public Guid Id { get; set; }
    public string NombreArchivo { get; set; } = null!;
    public string ArchivoUrl { get; set; } = null!;
    public string TipoMime { get; set; } = null!;
    public long TamanoBytes { get; set; }
    public Guid SubidoPorUsuarioId { get; set; }
    public string SubidoPorUsuarioNombre { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO para representar una evidencia en la tarea
/// </summary>
public class EvidenciaDTO
{
    public Guid Id { get; set; }
    public string? Descripcion { get; set; }
    public string? NombreArchivo { get; set; }
    public string? ArchivoUrl { get; set; }
    public string? TipoMime { get; set; }
    public long TamanoBytes { get; set; }
    public Guid SubidoPorUsuarioId { get; set; }
    public string SubidoPorUsuarioNombre { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO para agregar una nueva evidencia a una tarea
/// </summary>
public class AgregarEvidenciaDTO
{
    /// <summary>
    /// Descripción o texto de la evidencia (opcional si hay archivo)
    /// </summary>
    public string? Descripcion { get; set; }
}

/// <summary>
/// DTO para subir un documento adjunto a una tarea
/// </summary>
public class SubirDocumentoAdjuntoDTO
{
    // El archivo se envía como IFormFile en el controller
}
