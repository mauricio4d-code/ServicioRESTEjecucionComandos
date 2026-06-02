using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServicioRESTEjecucionComandos.Data;

namespace ServicioRESTEjecucionComandos.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the SQLite database used for refresh tokens and schedules.
/// </summary>
public class SqliteDbHealthCheck : IHealthCheck
{
    private readonly RefreshTokenDbContext _refreshTokenDbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDbHealthCheck"/> class.
    /// </summary>
    /// <param name="refreshTokenDbContext">The refresh token database context.</param>
    public SqliteDbHealthCheck(RefreshTokenDbContext refreshTokenDbContext)
    {
        _refreshTokenDbContext = refreshTokenDbContext;
    }

    /// <summary>
    /// Checks if the SQLite database is reachable.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <returns>A task representing the async operation containing the health status.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _refreshTokenDbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("SQLite database connection OK", new Dictionary<string, object>
                {
                    ["database"] = "RefreshTokenDatabase",
                    ["provider"] = _refreshTokenDbContext.Database.ProviderName!
                });
            }

            return HealthCheckResult.Unhealthy("SQLite database is not reachable", null!, new Dictionary<string, object>
            {
                ["database"] = "RefreshTokenDatabase",
                ["provider"] = _refreshTokenDbContext.Database.ProviderName!
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQLite database check failed", ex, new Dictionary<string, object>
            {
                ["database"] = "RefreshTokenDatabase",
                ["error"] = ex.Message
            });
        }
    }
}
