using System.ComponentModel.DataAnnotations;

namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// Request DTO for the refresh token endpoint.
/// </summary>
public class RefreshRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
