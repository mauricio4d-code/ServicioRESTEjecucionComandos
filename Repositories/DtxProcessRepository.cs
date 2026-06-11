using Microsoft.EntityFrameworkCore;
using System.Linq;
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
        var id = _context.Database.SqlQueryRaw<long>(
            sql,
            process.AppName,
            process.ProcessName,
            process.Status,
            process.StartTime,
            process.Active,
            detailsValue,
            process.CreatedAt
        ).AsEnumerable().FirstOrDefault();

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
    /// Checks whether any process records with status 'RUNNING' exist.
    /// Uses EXISTS to stop at the first match instead of materializing all rows.
    /// </summary>
    public virtual async Task<bool> AreRunningProcessesExistAsync()
    {
        _logger.LogDebug("Checking if RUNNING records exist in dtx_process.");

        var sql = @"SELECT CASE WHEN EXISTS (SELECT 1 FROM dtx_process WHERE status = 'RUNNING') THEN 1 ELSE 0 END";

        var result = _context.Database.SqlQueryRaw<int>(sql).AsEnumerable().FirstOrDefault();
        var exists = result == 1;

        _logger.LogDebug("RUNNING records exist in dtx_process: {Exists}.", exists);
        return exists;
    }

    /// <summary>
    /// Gets the IDs of all RUNNING process records whose start_time is older than the given threshold.
    /// Only selects id_process to minimize data transfer.
    /// </summary>
    public virtual async Task<List<long>> GetRunningProcessIdsOlderThanAsync(TimeSpan threshold)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(threshold);
        _logger.LogDebug("Querying dtx_process for RUNNING process IDs older than {Threshold} (cutoff: {Cutoff}).",
            threshold, cutoffTime);

        var sql = @"SELECT id_process FROM dtx_process
                    WHERE status = 'RUNNING' AND start_time < {0}";

        var results = _context.Database.SqlQueryRaw<long>(sql, cutoffTime).AsEnumerable().ToList();

        _logger.LogDebug("Found {Count} RUNNING process IDs older than threshold.", results.Count);
        return results;
    }
}
