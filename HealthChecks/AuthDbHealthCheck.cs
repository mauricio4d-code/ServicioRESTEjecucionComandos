using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServicioRESTEjecucionComandos.Data;

namespace ServicioRESTEjecucionComandos.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the legacy authentication database.
/// </summary>
public class AuthDbHealthCheck : IHealthCheck
{
    private readonly AuthDbContext _authDbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthDbHealthCheck"/> class.
    /// </summary>
    /// <param name="authDbContext">The authentication database context.</param>
    public AuthDbHealthCheck(AuthDbContext authDbContext)
    {
        _authDbContext = authDbContext;
    }

    /// <summary>
    /// Checks if the authentication database is reachable.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <returns>A task representing the async operation containing the health status.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _authDbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("Auth database connection OK", new Dictionary<string, object>
                {
                    ["database"] = "AuthDatabase",
                    ["provider"] = _authDbContext.Database.ProviderName!
                });
            }

            return HealthCheckResult.Unhealthy("Auth database is not reachable", null!, new Dictionary<string, object>
            {
                ["database"] = "AuthDatabase",
                ["provider"] = _authDbContext.Database.ProviderName!
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Auth database check failed", ex, new Dictionary<string, object>
            {
                ["database"] = "AuthDatabase",
                ["error"] = ex.Message
            });
        }
    }
}
