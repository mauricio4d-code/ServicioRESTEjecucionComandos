using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for raw SQL operations on the legacy 'dtx_process' table.
/// All queries use single round-trips to avoid N+1 problems.
/// </summary>
public class DtxProcessRepository
{
    private readonly ServiceDbContext _context;
    private readonly ILogger<DtxProcessRepository> _logger;

    /// <summary>
    /// Initializes a new instance of DtxProcessRepository.
    /// </summary>
    public DtxProcessRepository(ServiceDbContext context, ILogger<DtxProcessRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Inserts a new process record into dtx_process table.
    /// </summary>
    public virtual async Task<long> InsertAsync(DtxProcess process)
    {
        _logger.LogInformation("Inserting new dtx_process record for AppName {AppName}, ProcessName {ProcessName}.",
            process.AppName, process.ProcessName);

        var sql = @"INSERT INTO dtx_process (app_name, process_name, status, start_time, active, details, created_at)
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})
                    RETURNING id_process";

        var detailsValue = (object?)process.Details ?? DBNull.Value;
        var id = await _context.Database.SqlQueryRaw<long>(
            sql,
            process.AppName,
            process.ProcessName,
            process.Status,
            process.StartTime,
            process.Active,
            detailsValue,
            process.CreatedAt
        ).FirstOrDefaultAsync();

        _logger.LogInformation("dtx_process record inserted with IdProcess {IdProcess}.", id);
        return id;
    }

    /// <summary>
    /// Updates the status and optionally the end_time of a process record.
    /// </summary>
    public virtual async Task UpdateStatusAsync(long idProcess, string status, DateTime? endTime = null)
    {
        _logger.LogInformation("Updating dtx_process record {IdProcess} to status {Status}.", idProcess, status);

        if (endTime.HasValue)
        {
            var sql = @"UPDATE dtx_process SET status = {0}, end_time = {1} WHERE id_process = {2}";
            await _context.Database.ExecuteSqlRawAsync(
                sql,
                status,
                endTime.Value,
                idProcess
            );
        }
        else
        {
            var sql = @"UPDATE dtx_process SET status = {0} WHERE id_process = {1}";
            await _context.Database.ExecuteSqlRawAsync(
                sql,
                status,
                idProcess
            );
        }

        _logger.LogInformation("dtx_process record {IdProcess} updated successfully.", idProcess);
    }

    /// <summary>
    /// Gets all process records with status 'RUNNING'.
    /// Single query - no N+1 risk.
    /// </summary>
    public virtual async Task<List<DtxProcess>> GetRunningProcessesAsync()
    {
        _logger.LogDebug("Querying dtx_process for RUNNING records.");

        var sql = @"SELECT id_process, app_name, process_name, status, start_time, end_time, active, details, created_at
                    FROM dtx_process
                    WHERE status = 'RUNNING'";

        var results = await _context.Database.SqlQueryRaw<DtxProcess>(sql).ToListAsync();

        _logger.LogDebug("Found {Count} RUNNING records in dtx_process.", results.Count);
        return results;
    }

    /// <summary>
    /// Gets all RUNNING process records whose start_time is older than the given threshold.
    /// Single query - no N+1 risk.
    /// </summary>
    public virtual async Task<List<DtxProcess>> GetRunningProcessesOlderThanAsync(TimeSpan threshold)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(threshold);
        _logger.LogDebug("Querying dtx_process for RUNNING records older than {Threshold} (cutoff: {Cutoff}).",
            threshold, cutoffTime);

        var sql = @"SELECT id_process, app_name, process_name, status, start_time, end_time, active, details, created_at
                    FROM dtx_process
                    WHERE status = 'RUNNING' AND start_time < {0}";

        var results = await _context.Database.SqlQueryRaw<DtxProcess>(
            sql,
            cutoffTime
        ).ToListAsync();

        _logger.LogDebug("Found {Count} RUNNING records older than threshold.", results.Count);
        return results;
    }
}
