using System.Diagnostics;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Executes the Datax.SAFI.Downloader console application and captures the result.
/// Results are no longer written to disk; they are returned for database persistence.
/// </summary>
public class CommandExecutor
{
    // Error pattern constants for string matching
    private const string UnauthorizedErrorPattern = "error: Unauthorized";
    private const string NoApiKeyErrorPattern = "error: No API key found in request";
    private const string GenericErrorPattern = "error:";

    // Localized error message constants
    private const string UnauthorizedErrorMessage = "Los permisos para ejecutar el ETL no son validos. Por favor actualize sus credenciales.";
    private const string NoApiKeyErrorMessage = "No se encontro la llave API en el request. Por favor revise la configuracion para el ETL.";
    private const string GenericErrorMessage = "Ocurrio un error durante la ejecucion del ETL.";

    // Maximum length for output/error text stored in database
    private const int MaxOutputLength = 500;
    private const string TruncatedPrefix = "...";

    private readonly string _exePath;
    private readonly ILogger<CommandExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the CommandExecutor.
    /// </summary>
    public CommandExecutor(
        string exePath,
        ILogger<CommandExecutor> logger)
    {
        _exePath = exePath;
        _logger = logger;
    }

    /// <summary>
    /// Execution result returned after running the command.
    /// </summary>
    public class ExecutionResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Executes the console application with parameters from the queue item and returns the result.
    /// </summary>
    /// <param name="item">The queue item containing execution context and dynamic parameters.</param>
    /// <returns>ExecutionResult with exit code, output, and error information.</returns>
    public virtual async Task<ExecutionResult> ExecuteAsync(ExecutionQueueItem item)
    {
        var itemId = item.Id;
        var historyId = item.HistoryId;
        var arguments = string.IsNullOrWhiteSpace(item.Params) ? string.Empty : item.Params;

        _logger.LogInformation("Executing command for item {ItemId} (HistoryId {HistoryId}): {ExePath} {Arguments}",
            itemId, historyId, _exePath, arguments);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Read output and error streams asynchronously
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Wait patiently for the process to exit
            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;
            var exitCode = process.ExitCode;

            // Truncate output/error from the beginning to keep the last messages
            output = TruncateFromStart(output);
            error = TruncateFromStart(error);

            // Log severe ETL errors (exitCode == -1) with full stack trace for debugging
            if (exitCode == -1)
            {
                var etlErrorDetail = string.Empty;
                if (!string.IsNullOrEmpty(output))
                    etlErrorDetail += $"OUTPUT:\n{output}\n";
                if (!string.IsNullOrEmpty(error))
                    etlErrorDetail += $"ERROR:\n{error}\n";

                if (!string.IsNullOrEmpty(etlErrorDetail))
                {
                    _logger.LogError(
                        "============================================\n" +
                        "SEVERE ETL ERROR DETECTED (ExitCode: -1) for item {ItemId}.\n" +
                        "A critical error occurred inside the ETL process.\n" +
                        "Stack trace / error detail follows:\n\n" +
                        "{EtlErrorDetail}\n" +
                        "============================================",
                        itemId, etlErrorDetail);
                }
            }

            // Check for specific error patterns in output/error streams
            if (output.Contains(UnauthorizedErrorPattern) || error.Contains(UnauthorizedErrorPattern))
            {
                output = string.Empty; // Clear output to avoid confusion
                error = UnauthorizedErrorMessage;
                exitCode = -1; // Set a non-zero exit code to indicate failure
            }
            else if (output.Contains(NoApiKeyErrorPattern) || error.Contains(NoApiKeyErrorPattern))
            {
                output = string.Empty; // Clear output to avoid confusion
                error = NoApiKeyErrorMessage;
                exitCode = -1; // Set a non-zero exit code to indicate failure
            }
            else if (output.Contains(GenericErrorPattern) || error.Contains(GenericErrorPattern))
            {
                output = string.Empty; // Clear output to avoid confusion
                error = GenericErrorMessage;
                exitCode = -1; // Set a non-zero exit code to indicate failure
            }
            // Sanitize ExecutionResult: when exitCode is still -1 after pattern checks,
            // hide the raw ETL stack trace and point the caller to application logs.
            else if (exitCode == -1)
            {
                output = string.Empty;
                error = "Ocurrió un error grave dentro del ETL. Revise los logs de la aplicación para obtener más detalles.";
            }

            _logger.LogInformation(
                "============================================\n" +
                "Command execution completed for item {ItemId}. ExitCode: {ExitCode}\n" +
                "Output (truncated):\n{Output}" +
                "============================================",
                itemId, exitCode, output);

            // Keep only the last line of output for the end user
            output = GetLastLine(output);

            return new ExecutionResult
            {
                Success = exitCode == 0,
                ExitCode = exitCode,
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command for item {ItemId}", itemId);

            return new ExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Output = string.Empty,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Truncates a string from the beginning, keeping the last <paramref name="maxLength"/> characters.
    /// If the string exceeds the limit, a truncation marker is prepended.
    /// </summary>
    private string TruncateFromStart(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        if (input.Length <= MaxOutputLength)
            return input;

        var kept = input.Substring(input.Length - MaxOutputLength);
        return TruncatedPrefix + kept;
    }

    /// <summary>
    /// Returns the last non-empty line of the provided multi-line string.
    /// If the input is null or empty, returns an empty string.
    /// </summary>
    private string GetLastLine(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var lines = input.Split('\n', '\r');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                return lines[i].Trim();
        }

        return string.Empty;
    }
}