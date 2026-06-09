using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for persisting and querying Windows service restart history
/// in the local SQLite database.
/// </summary>
public class ServiceRestartLogRepository
{
    private readonly RefreshTokenDbContext _context;

    /// <summary>
    /// Initializes a new instance of ServiceRestartLogRepository.
    /// </summary>
    public ServiceRestartLogRepository(RefreshTokenDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves the most recent restart timestamp for the specified service.
    /// Returns <c>null</c> if no restart has been recorded.
    /// </summary>
    public async Task<DateTime?> GetLastRestartAsync(string serviceName)
    {
        var sql = @"
            SELECT ""RestartedAt""
            FROM ""service_restart_log""
            WHERE ""ServiceName"" = @serviceName
            ORDER BY ""RestartedAt"" DESC
            LIMIT 1";

        var results = await _context
            .Database
            .SqlQueryRaw<DateTime?>(sql,
                new Microsoft.Data.Sqlite.SqliteParameter("@serviceName", serviceName))
            .ToListAsync();

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Persists a new restart event to the local SQLite database.
    /// </summary>
    public async Task SaveRestartAsync(ServiceRestartLog log)
    {
        var sql = @"
            INSERT INTO ""service_restart_log"" (""ServiceName"", ""RestartedAt"", ""Status"")
            VALUES (@serviceName, @restartedAt, @status)";

        await _context.Database.ExecuteSqlRawAsync(sql,
            new Microsoft.Data.Sqlite.SqliteParameter("@serviceName", log.ServiceName),
            new Microsoft.Data.Sqlite.SqliteParameter("@restartedAt", log.RestartedAt.ToString("O")),
            new Microsoft.Data.Sqlite.SqliteParameter("@status", log.Status));
    }
}
