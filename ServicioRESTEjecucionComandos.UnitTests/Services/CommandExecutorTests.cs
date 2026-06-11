using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Services;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Services;

public class CommandExecutorTests
{
    private readonly Mock<ILogger<CommandExecutor>> _loggerMock;

    public CommandExecutorTests()
    {
        _loggerMock = new Mock<ILogger<CommandExecutor>>();
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ShouldReturnSuccess()
    {
        // Arrange - Use PowerShell to run a simple command
        var executor = new CommandExecutor(
            "powershell.exe",
            _loggerMock.Object);

        var item = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = "-Command \"Write-Output 'Hello World'\"",
            Status = "PENDIENTE"
        };

        // Act
        var result = await executor.ExecuteAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("Hello World");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyParams_ShouldExecuteWithoutArguments()
    {
        // Arrange
        var executor = new CommandExecutor(
            "hostname.exe",     // Without arguments, print the PC name and end.
            _loggerMock.Object);

        var item = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = string.Empty,
            Status = "PENDIENTE"
        };

        // Act
        var result = await executor.ExecuteAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_NullParams_ShouldExecuteWithoutArguments()
    {
        // Arrange
        var executor = new CommandExecutor(
            "hostname.exe",     // Without arguments, print the PC name and end.
            _loggerMock.Object);

        var item = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = null,
            Status = "PENDIENTE"
        };

        // Act
        var result = await executor.ExecuteAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_CommandWithError_ShouldReturnFailure()
    {
        // Arrange
        var executor = new CommandExecutor(
            "powershell.exe",
            _loggerMock.Object);

        var item = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = "-Command \"exit 1\"",
            Status = "PENDIENTE"
        };

        // Act
        var result = await executor.ExecuteAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentExecutable_ShouldReturnFailure()
    {
        // Arrange
        var executor = new CommandExecutor(
            "C:\\NonExistent\\Path\\fake_executable.exe",
            _loggerMock.Object);

        var item = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = "",
            Status = "PENDIENTE"
        };

        // Act
        var result = await executor.ExecuteAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCaptureStandardError()
    {
        // Arrange
        var executor = new CommandExecutor(
            "powershell.exe",
            _loggerMock.Object);

        var item = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = "-Command \"Write-Error 'Test error' -WarningAction SilentlyContinue; exit 1\"",
            Status = "PENDIENTE"
        };

        // Act
        var result = await executor.ExecuteAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleExecutions_ShouldWorkIndependently()
    {
        // Arrange
        var executor = new CommandExecutor(
            "powershell.exe",
            _loggerMock.Object);

        var item1 = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = "-Command \"Write-Output 'First'\"",
            Status = "PENDIENTE"
        };

        var item2 = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = "-Command \"Write-Output 'Second'\"",
            Status = "PENDIENTE"
        };

        // Act
        var result1 = await executor.ExecuteAsync(item1);
        var result2 = await executor.ExecuteAsync(item2);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result1.Output.Should().Contain("First");
        result2.Output.Should().Contain("Second");
    }

    [Fact]
    public async Task ExecuteAsync_LargeOutput_ShouldTruncateFromStart()
    {
        // Arrange
        var executor = new CommandExecutor(
            "powershell.exe",
            _loggerMock.Object);

        // Generate a large output by repeating a string many times, ending with a unique marker
        var largePayload = "A".PadLeft(5000, 'A');
        var marker = "END_MARKER";
        var item = new ExecutionQueueItem
        {
            Id = Guid.NewGuid(),
            HistoryId = Guid.NewGuid(),
            Params = $"-Command \"Write-Output '{largePayload}'; Write-Output '{marker}'\"",
            Status = "PENDIENTE"
        };

        // Act
        var result = await executor.ExecuteAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        // Output should contain the truncation prefix
        result.Output.Should().Contain("[truncado]");
        // Output should end with the marker (last messages are preserved)
        result.Output.Should().Contain(marker);
        // Total length should be within bounds (MaxOutputLength + prefix length)
        result.Output.Length.Should().BeLessOrEqualTo(500 + 15);
    }
}
