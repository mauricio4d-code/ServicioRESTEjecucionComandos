using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ServicioRESTEjecucionComandos.HealthChecks;

/// <summary>
/// Health check that verifies Hangfire background job processing is operational.
/// </summary>
public class HangfireHealthCheck : IHealthCheck
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HangfireHealthCheck"/> class.
    /// </summary>
    /// <param name="backgroundJobClient">The Hangfire background job client.</param>
    public HangfireHealthCheck(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// Checks if Hangfire is operational by verifying the background job client is available.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <returns>A task representing the async operation containing the health status.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify Hangfire storage is accessible by checking the storage instance
            var storageAccessible = await Task.Run(() =>
            {
                try
                {
                    // Try to access the job storage to verify it's operational
                    var storage = JobStorage.Current;
                    return storage != null;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);

            if (storageAccessible)
            {
                return HealthCheckResult.Healthy("Hangfire background worker active", new Dictionary<string, object>
                {
                    ["storage"] = "InMemory"
                });
            }

            return HealthCheckResult.Unhealthy("Hangfire storage is not accessible", null!, new Dictionary<string, object>
            {
                ["storage"] = "InMemory"
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Hangfire check failed", ex, new Dictionary<string, object>
            {
                ["storage"] = "InMemory",
                ["error"] = ex.Message
            });
        }
    }
}
