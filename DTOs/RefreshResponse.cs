namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// Response DTO for the refresh token endpoint.
/// Reuses LoginResponse structure.
/// </summary>
public class RefreshResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
