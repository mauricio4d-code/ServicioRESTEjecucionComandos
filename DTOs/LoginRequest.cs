using System.ComponentModel.DataAnnotations;

namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// Request DTO for the login endpoint.
/// </summary>
public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
