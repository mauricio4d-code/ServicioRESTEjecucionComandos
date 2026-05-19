namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// Response DTO for error messages.
/// </summary>
public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
