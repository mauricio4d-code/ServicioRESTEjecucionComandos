using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using ServicioRESTEjecucionComandos.Hubs;
using ServicioRESTEjecucionComandos.Services;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Services;

public class ExecutionNotifierTests
{
    private readonly Mock<IHubContext<EtlNotificationHub>> _mockHubContext;
    private readonly ExecutionNotifier _notifier;

    public ExecutionNotifierTests()
    {
        _mockHubContext = new Mock<IHubContext<EtlNotificationHub>>();
        // Setup Clients.All to return a dummy IClientProxy that doesn't throw
        var mockClientProxy = new Mock<IClientProxy>();
        _mockHubContext.Setup(x => x.Clients.All).Returns(mockClientProxy.Object);
        _notifier = new ExecutionNotifier(_mockHubContext.Object);
    }

    [Fact]
    public async Task BroadcastTaskStartedAsync_ShouldCompleteWithoutThrowing()
    {
        // Arrange
        string testParams = "--codigo=TEST --cod_envio=123";

        // Act
        Func<Task> act = () => _notifier.BroadcastTaskStartedAsync(testParams);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastTaskStartedAsync_WithNullParams_ShouldCompleteWithoutThrowing()
    {
        // Act
        Func<Task> act = () => _notifier.BroadcastTaskStartedAsync(null);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastTaskCompletedAsync_WithSuccess_ShouldCompleteWithoutThrowing()
    {
        // Arrange
        string testParams = "--codigo=TEST --cod_envio=123";

        // Act
        Func<Task> act = () => _notifier.BroadcastTaskCompletedAsync(true, testParams);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastTaskCompletedAsync_WithFailure_ShouldCompleteWithoutThrowing()
    {
        // Arrange
        string testParams = "--codigo=TEST --cod_envio=123";

        // Act
        Func<Task> act = () => _notifier.BroadcastTaskCompletedAsync(false, testParams);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastTaskCompletedAsync_WithNullParams_ShouldCompleteWithoutThrowing()
    {
        // Act
        Func<Task> act = () => _notifier.BroadcastTaskCompletedAsync(true, null);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastTaskStartedAsync_MultipleCalls_ShouldCompleteWithoutThrowing()
    {
        // Act
        Func<Task> act = async () =>
        {
            await _notifier.BroadcastTaskStartedAsync("--codigo=TEST1");
            await _notifier.BroadcastTaskStartedAsync("--codigo=TEST2");
            await _notifier.BroadcastTaskStartedAsync("--codigo=TEST3");
        };

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastTaskCompletedAsync_ShouldHandleBothSuccessAndFailure()
    {
        // Act & Assert - Verify success=true completes
        Func<Task> actSuccess = () => _notifier.BroadcastTaskCompletedAsync(true, "--codigo=TEST");
        await actSuccess.Should().NotThrowAsync();

        // Verify success=false completes
        Func<Task> actFailure = () => _notifier.BroadcastTaskCompletedAsync(false, "--codigo=TEST");
        await actFailure.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastTaskStartedAsync_ShouldCallClientsAll()
    {
        // Act
        await _notifier.BroadcastTaskStartedAsync("--codigo=TEST");

        // Assert - verify Clients.All was accessed
        _mockHubContext.Verify(x => x.Clients.All, Times.Once);
    }

    [Fact]
    public async Task BroadcastTaskCompletedAsync_ShouldCallClientsAll()
    {
        // Act
        await _notifier.BroadcastTaskCompletedAsync(true, "--codigo=TEST");

        // Assert - verify Clients.All was accessed
        _mockHubContext.Verify(x => x.Clients.All, Times.Once);
    }
}
