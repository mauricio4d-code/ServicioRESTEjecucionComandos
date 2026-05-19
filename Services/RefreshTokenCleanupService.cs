using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServicioRESTEjecucionComandos.Repositories;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Background service that periodically cleans up expired refresh tokens and old audit logs.
/// </summary>
public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval;
    private readonly int _auditLogRetentionDays;

    public RefreshTokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<RefreshTokenCleanupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var cleanupMinutes = configuration.GetValue<int>("RefreshTokenCleanup:CleanupIntervalMinutes", 60);
        _cleanupInterval = TimeSpan.FromMinutes(cleanupMinutes);

        _auditLogRetentionDays = configuration.GetValue<int>("RefreshTokenCleanup:AuditLogRetentionDays", 90);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RefreshTokenCleanupService started. Interval: {Interval}min, Audit retention: {Days}days",
            _cleanupInterval.TotalMinutes, _auditLogRetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup cycle");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("RefreshTokenCleanupService stopped");
    }

    private async Task PerformCleanupAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<RefreshTokenRepository>();

        var utcNow = DateTime.UtcNow;
        var cutoffDate = utcNow.AddDays(-_auditLogRetentionDays);

        // Clean expired refresh tokens
        var expiredCount = await repository.DeleteExpiredAsync(utcNow);
        _logger.LogInformation("Cleanup: Removed {Count} expired refresh tokens", expiredCount);

        // Clean old audit logs
        var oldLogsCount = await repository.DeleteOldAuditLogsAsync(cutoffDate);
        _logger.LogInformation("Cleanup: Removed {Count} audit logs older than {Days} days", 
            oldLogsCount, _auditLogRetentionDays);

        _logger.LogInformation("Cleanup cycle completed. Removed {TokenCount} tokens and {LogCount} logs",
            expiredCount, oldLogsCount);
    }
}
