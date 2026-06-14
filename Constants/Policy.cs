namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// Authorization policy names used across controllers and middleware.
/// </summary>
public static class Policy
{
    /// <summary>
    /// Policy requiring the user to have the Administrador role.
    /// </summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Rate limiting policy applied to the login endpoint.
    /// </summary>
    public const string LoginPolicy = "LoginPolicy";
}
