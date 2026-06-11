using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// DTO for the result of the dtx_seguimiento verification query.
/// </summary>
public class DtxSeguimientoVerificationResult
{
    /// <summary>
    /// The cod_envio from the matching record.
    /// </summary>
    public string CodEnvio { get; set; } = string.Empty;

    /// <summary>
    /// The latest fechadatos found for this cod_envio and codigo combination.
    /// </summary>
    public DateOnly? FechaDatos { get; set; }
}

/// <summary>
/// Repository for CRUD operations on ETLExecutionHistory entities.
/// </summary>
public class ETLExecutionHistoryRepository
{
    private readonly ServiceDbContext _context;
    private readonly ILogger<ETLExecutionHistoryRepository> _logger;

    /// <summary>
    /// Initializes a new instance of ETLExecutionHistoryRepository.
    /// </summary>
    public ETLExecutionHistoryRepository(ServiceDbContext context, ILogger<ETLExecutionHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new ETLExecutionHistory record and saves it to the database.
    /// </summary>
    public async Task<ETLExecutionHistory> CreateAsync(ETLExecutionHistory item)
    {
        item.Id = Guid.NewGuid();
        item.Status = "PENDIENTE";
        _logger.LogInformation("Creating new ETLExecutionHistory record in database for Codigo {Codigo}, CodEnvio {CodEnvio}.", item.Codigo, item.CodEnvio);
        await _context.ETLExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETLExecutionHistory record created in database with Id {HistoryId} and status PENDIENTE.", item.Id);
        return item;
    }

    /// <summary>
    /// Gets an ETLExecutionHistory record by its Id.
    /// </summary>
    public virtual async Task<ETLExecutionHistory?> GetByIdAsync(Guid id)
    {
        _logger.LogDebug("Querying ETLExecutionHistory from database by Id {HistoryId}.", id);
        var result = await _context.ETLExecutionHistories.FindAsync(id);
        _logger.LogDebug("ETLExecutionHistory query by Id {HistoryId} returned {Found}.", id, result != null);
        return result;
    }

    /// <summary>
    /// Updates an existing ETLExecutionHistory record and persists changes.
    /// </summary>
    public async Task UpdateAsync(ETLExecutionHistory item)
    {
        _logger.LogInformation("Updating ETLExecutionHistory record in database with Id {HistoryId}.", item.Id);
        _context.ETLExecutionHistories.Update(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETLExecutionHistory record updated in database with Id {HistoryId}.", item.Id);
    }

    /// <summary>
    /// Updates the status and related fields of an ETLExecutionHistory record atomically.
    /// </summary>
    public virtual async Task UpdateStatusAsync(Guid id, string status, int? exitCode = null, string? output = null, string? error = null, DateTime? executedAt = null, DateTime? completedAt = null)
    {
        _logger.LogInformation("Updating status to {Status} for ETLExecutionHistory in database with Id {HistoryId}.", status, id);
        var item = await _context.ETLExecutionHistories.FindAsync(id);
        if (item == null)
        {
            _logger.LogWarning("Cannot update status: ETLExecutionHistory not found with Id {HistoryId}.", id);
            return;
        }

        item.Status = status;
        if (exitCode.HasValue) item.ExitCode = exitCode;
        if (output != null) item.Output = output;
        if (error != null) item.Error = error;
        if (executedAt.HasValue) item.ExecutedAt = executedAt;
        if (completedAt.HasValue) item.CompletedAt = completedAt;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Status updated to {Status} for ETLExecutionHistory in database with Id {HistoryId}.", status, id);
    }

    /// <summary>
    /// Gets all ETLExecutionHistory records, ordered by creation time descending.
    /// </summary>
    public async Task<List<ETLExecutionHistory>> GetAllAsync()
    {
        _logger.LogDebug("Querying all ETLExecutionHistory records from database.");
        var result = await _context.ETLExecutionHistories.OrderByDescending(x => x.Id).ToListAsync();
        _logger.LogDebug("Retrieved {Count} ETLExecutionHistory records from database.", result.Count);
        return result;
    }

    /// <summary>
    /// Gets ETLExecutionHistory records filtered by status.
    /// </summary>
    public async Task<List<ETLExecutionHistory>> GetByStatusAsync(string status)
    {
        _logger.LogDebug("Querying ETLExecutionHistory records from database filtered by status {Status}.", status);
        var result = await _context.ETLExecutionHistories.Where(x => x.Status == status).ToListAsync();
        _logger.LogDebug("Retrieved {Count} ETLExecutionHistory records with status {Status} from database.", result.Count, status);
        return result;
    }

    /// <summary>
    /// Gets all active (PENDIENTE or EN PROCESO) ETLExecutionHistory records.
    /// Used by the batch status polling endpoint to return all in-progress executions in a single query.
    /// </summary>
    public virtual async Task<List<ETLExecutionHistory>> GetAllActiveAsync()
    {
        _logger.LogDebug("Querying all active ETLExecutionHistory records from database.");
        var result = await _context.ETLExecutionHistories
            .Where(x => x.Status == "PENDIENTE" || x.Status == "EN PROCESO")
            .ToListAsync();
        _logger.LogDebug("Retrieved {Count} active ETLExecutionHistory records from database.", result.Count);
        return result;
    }

    /// <summary>
    /// Gets the first active (PENDIENTE or EN PROCESO) execution for the given CodEnvio and Codigo, if any.
    /// </summary>
    public async Task<ETLExecutionHistory?> GetActiveExecutionAsync(string codEnvio, string codigo)
    {
        _logger.LogDebug("Querying active ETLExecutionHistory from database for CodEnvio {CodEnvio}, Codigo {Codigo}.", codEnvio, codigo);
        var result = await _context.ETLExecutionHistories
            .FirstOrDefaultAsync(x => x.CodEnvio == codEnvio
                && x.Codigo == codigo
                && (x.Status == "PENDIENTE" || x.Status == "EN PROCESO"));
        _logger.LogDebug("Active ETLExecutionHistory query for CodEnvio {CodEnvio}, Codigo {Codigo} returned {Found}.", codEnvio, codigo, result != null);
        return result;
    }

    /// <summary>
    /// Atomically checks for an active execution and creates a new record if none exists.
    /// Returns the existing active record if found, or the newly created record otherwise.
    /// This eliminates the N+1 check-then-insert pattern by performing both operations
    /// in a single database scope with a single save.
    /// On unique constraint violation (race condition), re-queries and returns the existing active record.
    /// </summary>
    public virtual async Task<ETLExecutionHistory> UpsertOrGetActiveAsync(
        string codEnvio,
        string codigo,
        string tipoEntidad,
        DateOnly fechaDatos,
        string triggerType = "MANUAL")
    {
        // Check for existing active execution first
        var existing = await _context.ETLExecutionHistories
            .FirstOrDefaultAsync(x => x.CodEnvio == codEnvio
                && x.Codigo == codigo
                && (x.Status == "PENDIENTE" || x.Status == "EN PROCESO"));

        if (existing != null)
        {
            _logger.LogWarning(
                "Active execution already exists for CodEnvio={CodEnvio}, Codigo={Codigo}. " +
                "Returning existing HistoryId={HistoryId} with status {Status}.",
                codEnvio, codigo, existing.Id, existing.Status);
            return existing;
        }

        // Create new record
        var history = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = codEnvio,
            TipoEntidad = tipoEntidad,
            FechaDatos = fechaDatos,
            Codigo = codigo,
            Status = "PENDIENTE",
            TriggerType = triggerType
        };

        _logger.LogInformation(
            "Creating new ETLExecutionHistory record in database for Codigo {Codigo}, CodEnvio {CodEnvio}.",
            codigo, codEnvio);

        try
        {
            await _context.ETLExecutionHistories.AddAsync(history);
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "ETLExecutionHistory record created in database with Id {HistoryId} and status PENDIENTE.",
                history.Id);
            return history;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex,
                "Unique constraint violation creating ETLExecutionHistory for CodEnvio={CodEnvio}, Codigo={Codigo}. " +
                "Another concurrent request created it first. Re-querying for existing active record.",
                codEnvio, codigo);

            // Re-query to get the record created by the other request
            var concurrentRecord = await _context.ETLExecutionHistories
                .FirstOrDefaultAsync(x => x.CodEnvio == codEnvio
                    && x.Codigo == codigo
                    && (x.Status == "PENDIENTE" || x.Status == "EN PROCESO"));

            if (concurrentRecord != null)
            {
                _logger.LogInformation(
                    "Returning existing active ETLExecutionHistory with Id {HistoryId} after constraint violation.",
                    concurrentRecord.Id);
                return concurrentRecord;
            }

            // If we still can't find it, re-throw the original exception
            _logger.LogError(ex,
                "Unique constraint violation for CodEnvio={CodEnvio}, Codigo={Codigo}, but no active record found after re-query.",
                codEnvio, codigo);
            throw;
        }
    }

    /// <summary>
    /// Determines if the given exception represents a unique constraint violation.
    /// Handles PostgreSQL (SQL state 23505), SQL Server (error 2601/2627), and generic EF Core violations.
    /// </summary>
    private static bool IsUniqueConstraintViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner == null)
            return false;

        // PostgreSQL: SQL state 23505 = unique_violation
        if (inner is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
            return true;

        // SQL Server: error codes 2601 (duplicate key) or 2627 (unique constraint violation)
        if (inner is Microsoft.Data.SqlClient.SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            return true;

        // SQLite / InMemory: check message for common unique constraint patterns
        var message = inner.Message.ToLowerInvariant();
        return message.Contains("unique") || message.Contains("duplicate");
    }

    /// <summary>
    /// Queries dtx_seguimiento to verify that a record exists for the given CodEnvio and Codigo combination.
    /// Returns the latest FechaDatos found, or null if no matching record exists.
    /// </summary>
    public virtual async Task<DtxSeguimientoVerificationResult?> VerifyDtxSeguimientoAsync(string codEnvio, string codigo)
    {
        _logger.LogInformation("[DB] Querying dtx_seguimiento for CodEnvio={CodEnvio}, Codigo={Codigo}.", codEnvio, codigo);
        var results = await _context.Database
            .SqlQueryRaw<DtxSeguimientoVerificationResult>(
                @"SELECT
                    s.cod_envio AS ""CodEnvio"",
                    MAX(s.fechadatos) AS ""FechaDatos""
                  FROM dtx_seguimiento s
                  WHERE s.cod_envio = {0}
                    AND s.codigo = {1}
                  GROUP BY s.cod_envio",
                codEnvio, codigo)
            .ToListAsync();

        var result = results.FirstOrDefault();
        _logger.LogInformation("[DB] dtx_seguimiento verification for CodEnvio={CodEnvio}, Codigo={Codigo} returned FechaDatos={FechaDatos}.",
            codEnvio, codigo, result?.FechaDatos);
        return result;
    }

    /// <summary>
    /// Updates the status, FechaDatos, and related fields of an ETLExecutionHistory record atomically.
    /// </summary>
    public virtual async Task UpdateStatusWithFechaDatosAsync(
        Guid id,
        string status,
        DateOnly? fechaDatos = null,
        int? exitCode = null,
        string? output = null,
        string? error = null,
        DateTime? executedAt = null,
        DateTime? completedAt = null)
    {
        _logger.LogInformation("Updating status to {Status} for ETLExecutionHistory in database with Id {HistoryId}.", status, id);
        var item = await _context.ETLExecutionHistories.FindAsync(id);
        if (item == null)
        {
            _logger.LogWarning("Cannot update status: ETLExecutionHistory not found with Id {HistoryId}.", id);
            return;
        }

        item.Status = status;
        if (fechaDatos.HasValue) item.FechaDatos = fechaDatos.Value;
        if (exitCode.HasValue) item.ExitCode = exitCode;
        if (output != null) item.Output = output;
        if (error != null) item.Error = error;
        if (executedAt.HasValue) item.ExecutedAt = executedAt;
        if (completedAt.HasValue) item.CompletedAt = completedAt;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Status updated to {Status} for ETLExecutionHistory in database with Id {HistoryId}.", status, id);
    }
}
