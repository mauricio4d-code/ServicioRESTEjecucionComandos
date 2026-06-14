namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// Controller action identifiers used in API request/response handling.
/// </summary>
public static class ControllerAction
{
    /// <summary>
    /// Action for updating/creating a new ETL execution.
    /// </summary>
    public const string Actualizar = "ACTUALIZAR";

    /// <summary>
    /// Action for re-processing an existing ETL execution.
    /// </summary>
    public const string Reprocesar = "REPROCESAR";
}
