using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ServicioRESTEjecucionComandos.HealthChecks;

/// <summary>
/// Health check that reports system-level information including uptime and memory usage.
/// </summary>
public class UptimeHealthCheck : IHealthCheck
{
    private static readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>
    /// Checks system health and reports uptime and memory statistics.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <returns>A task representing the async operation containing the health status.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var uptime = DateTime.UtcNow - _startTime;
        var memoryMB = GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0);

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Service running for {uptime}",
            new Dictionary<string, object>
            {
                ["uptime"] = uptime.ToString(),
                ["uptime_seconds"] = (long)uptime.TotalSeconds,
                ["memory_mb"] = Math.Round(memoryMB, 2),
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            }));
    }
}
