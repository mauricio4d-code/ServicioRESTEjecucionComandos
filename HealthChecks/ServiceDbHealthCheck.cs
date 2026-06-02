using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServicioRESTEjecucionComandos.Data;

namespace ServicioRESTEjecucionComandos.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the service database (PostgreSQL / SQL Server / SQLite).
/// </summary>
public class ServiceDbHealthCheck : IHealthCheck
{
    private readonly ServiceDbContext _serviceDbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceDbHealthCheck"/> class.
    /// </summary>
    /// <param name="serviceDbContext">The service database context.</param>
    public ServiceDbHealthCheck(ServiceDbContext serviceDbContext)
    {
        _serviceDbContext = serviceDbContext;
    }

    /// <summary>
    /// Checks if the service database is reachable.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <returns>A task representing the async operation containing the health status.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _serviceDbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("Service database connection OK", new Dictionary<string, object>
                {
                    ["database"] = "ServiceDatabase",
                    ["provider"] = _serviceDbContext.Database.ProviderName!
                });
            }

            return HealthCheckResult.Unhealthy("Service database is not reachable", null!, new Dictionary<string, object>
            {
                ["database"] = "ServiceDatabase",
                ["provider"] = _serviceDbContext.Database.ProviderName!
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Service database check failed", ex, new Dictionary<string, object>
            {
                ["database"] = "ServiceDatabase",
                ["error"] = ex.Message
            });
        }
    }
}
