using System.Diagnostics;
using System.Text.Json;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Executes the Datax.SAFI.Downloader console application and captures the result.
/// </summary>
public class CommandExecutor
{
    private readonly string _exePath;
    private readonly string _code;
    private readonly string _start;
    private readonly string _end;
    private readonly string _codesend;
    private readonly string _resultPath;
    private readonly ILogger<CommandExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the CommandExecutor.
    /// </summary>
    public CommandExecutor(
        string exePath,
        string code,
        string start,
        string end,
        string codesend,
        string resultPath,
        ILogger<CommandExecutor> logger)
    {
        _exePath = exePath;
        _code = code;
        _start = start;
        _end = end;
        _codesend = codesend;
        _resultPath = resultPath;
        _logger = logger;
    }

    /// <summary>
    /// Executes the console application with the configured parameters and writes the result to a JSON file.
    /// </summary>
    /// <param name="item">The queue item containing execution context.</param>
    /// <returns>True if execution succeeded; otherwise, false.</returns>
    public async Task<bool> ExecuteAsync(ExecutionQueueItem item)
    {
        var itemId = item.Id;
        var resultFileName = $"ETLResult_{itemId}.json";
        var resultFilePath = Path.Combine(_resultPath, resultFileName);

        _logger.LogInformation("Executing command for item {ItemId}: {ExePath} -code {Code} -start {Start} -end {End} -codesend {Codesend}",
            itemId, _exePath, _code, _start, _end, _codesend);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = $"-code {_code} -start {_start} -end {_end} -codesend {_codesend}",
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

            var result = new
            {
                ItemId = itemId,
                Status = exitCode == 0 ? "Success" : "Failed",
                ExitCode = exitCode,
                Output = output,
                Error = error,
                ExecutedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            // Ensure the output directory exists
            Directory.CreateDirectory(_resultPath);

            // Write result to JSON file
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var jsonContent = JsonSerializer.Serialize(result, jsonOptions);
            await File.WriteAllTextAsync(resultFilePath, jsonContent);

            // Update the item with results
            item.Status = exitCode == 0 ? "Completed" : "Failed";
            item.Result = exitCode == 0 ? output : error;
            item.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Command execution completed for item {ItemId}. ExitCode: {ExitCode}. Result written to {ResultPath}",
                itemId, exitCode, resultFilePath);

            return exitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command for item {ItemId}", itemId);

            var errorResult = new
            {
                ItemId = itemId,
                Status = "Error",
                ErrorMessage = ex.Message,
                ExecutedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            Directory.CreateDirectory(_resultPath);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var jsonContent = JsonSerializer.Serialize(errorResult, jsonOptions);
            await File.WriteAllTextAsync(resultFilePath, jsonContent);

            item.Status = "Error";
            item.Result = ex.Message;
            item.CompletedAt = DateTime.UtcNow;

            return false;
        }
    }
}