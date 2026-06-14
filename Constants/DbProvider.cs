namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// Database provider identifiers used for configuring DbContext connections.
/// </summary>
public static class DbProvider
{
    /// <summary>
    /// PostgreSQL database provider (short form).
    /// </summary>
    public const string Postgres = "postgres";

    /// <summary>
    /// PostgreSQL database provider (full form).
    /// </summary>
    public const string PostgreSQL = "postgresql";

    /// <summary>
    /// Microsoft SQL Server database provider.
    /// </summary>
    public const string SqlServer = "sqlserver";

    /// <summary>
    /// SQLite database provider.
    /// </summary>
    public const string Sqlite = "sqlite";
}
