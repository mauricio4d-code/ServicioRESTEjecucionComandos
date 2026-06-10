using System.Diagnostics;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Executes the Datax.SAFI.Downloader console application and captures the result.
/// Results are no longer written to disk; they are returned for database persistence.
/// Supports exponential retry with configurable attempts and delays.
/// </summary>
public class CommandExecutor
{
    private readonly string _exePath;
    private readonly ILogger<CommandExecutor> _logger;
    private readonly int _maxAttempts;
    private readonly TimeSpan[] _retryDelays;

    /// <summary>
    /// Initializes a new instance of the CommandExecutor.
    /// </summary>
    public CommandExecutor(
        string exePath,
        ILogger<CommandExecutor> logger)
    {
        _exePath = exePath;
        _logger = logger;
        _maxAttempts = 3;
        _retryDelays = new[] { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(9) };
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
    /// Retries up to 3 times with exponential backoff (3s, 9s) on failure.
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

        ExecutionResult lastResult = null;

        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                var delay = _retryDelays[attempt - 2];
                _logger.LogWarning(
                    "Command execution attempt {Attempt}/{MaxAttempts} failed for item {ItemId}. Retrying in {DelaySeconds}s.",
                    attempt - 1, _maxAttempts, itemId, delay.TotalSeconds);
                await Task.Delay(delay);
            }

            _logger.LogInformation("Command execution attempt {Attempt}/{MaxAttempts} for item {ItemId}.",
                attempt, _maxAttempts, itemId);

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

                lastResult = new ExecutionResult
                {
                    Success = exitCode == 0,
                    ExitCode = exitCode,
                    Output = output,
                    Error = error
                };

                if (lastResult.Success)
                {
                    _logger.LogInformation("Command execution completed successfully for item {ItemId} on attempt {Attempt}. ExitCode: {ExitCode}",
                        itemId, attempt, exitCode);
                    return lastResult;
                }

                _logger.LogWarning(
                    "Command execution attempt {Attempt}/{MaxAttempts} failed for item {ItemId}. ExitCode: {ExitCode}",
                    attempt, _maxAttempts, itemId, exitCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command for item {ItemId} on attempt {Attempt}", itemId, attempt);

                lastResult = new ExecutionResult
                {
                    Success = false,
                    ExitCode = -1,
                    Output = string.Empty,
                    Error = ex.Message
                };
            }
        }

        _logger.LogError(
            "Command execution failed for item {ItemId} after {MaxAttempts} attempts. Final ExitCode: {ExitCode}",
            itemId, _maxAttempts, lastResult?.ExitCode);

        return lastResult;
    }
}
