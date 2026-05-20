using System.Diagnostics;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Executes the Datax.SAFI.Downloader console application and captures the result.
/// Results are no longer written to disk; they are returned for database persistence.
/// </summary>
public class CommandExecutor
{
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
    public async Task<ExecutionResult> ExecuteAsync(ExecutionQueueItem item)
    {
        var itemId = item.Id;
        var serviceItemId = item.ServiceItemId;

        _logger.LogInformation("Executing command for item {ItemId} (ServiceItem {ServiceItemId}): {ExePath} -code {Code} -start {Start} -end {End} -codesend {Codesend}",
            itemId, serviceItemId, _exePath, item.Code, item.Start, item.End, item.Codesend);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = $"-code {item.Code} -start {item.Start} -end {item.End} -codesend {item.Codesend}",
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

            _logger.LogInformation("Command execution completed for item {ItemId}. ExitCode: {ExitCode}",
                itemId, exitCode);

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
}
