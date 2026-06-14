namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// Role names used for authorization checks.
/// </summary>
public static class Role
{
    /// <summary>
    /// Administrator role name.
    /// </summary>
    public const string Administrador = "Administrador";

    /// <summary>
    /// Typo variant of Administrador role for backward compatibility.
    /// The AdminOnly policy accepts both "Administrador" and "Administador".
    /// </summary>
    public const string AdministadorTypo = "Administador";

    /// <summary>
    /// Standard user role name.
    /// </summary>
    public const string Usuario = "Usuario";
}
