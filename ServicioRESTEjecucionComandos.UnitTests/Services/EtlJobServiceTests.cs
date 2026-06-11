using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;
using ServicioRESTEjecucionComandos.Services;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Services;

public class EtlJobServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ETLExecutionHistoryRepository> _historyRepoMock;
    private readonly Mock<DtxProcessRepository> _dtxProcessRepoMock;
    private readonly Mock<CommandExecutor> _executorMock;
    private readonly Mock<ILogger<EtlJobService>> _loggerMock;
    private readonly Mock<ExecutionNotifier> _notifierMock;
    private readonly IConfiguration _configuration;
    private readonly List<ETLExecutionHistory> _historyStore = new();

    public EtlJobServiceTests()
    {
        var executorLoggerMock = new Mock<ILogger<CommandExecutor>>();
        _executorMock = new Mock<CommandExecutor>("dummy.exe", executorLoggerMock.Object);
        _loggerMock = new Mock<ILogger<EtlJobService>>();

        var hubContextMock = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<ServicioRESTEjecucionComandos.Hubs.EtlNotificationHub>>();
        _notifierMock = new Mock<ExecutionNotifier>(hubContextMock.Object);

        var repoLoggerMock = new Mock<ILogger<ETLExecutionHistoryRepository>>();
        _historyRepoMock = new Mock<ETLExecutionHistoryRepository>(null!, repoLoggerMock.Object);

        var dtxProcessLoggerMock = new Mock<ILogger<DtxProcessRepository>>();
        _dtxProcessRepoMock = new Mock<DtxProcessRepository>(null!, dtxProcessLoggerMock.Object);
        _dtxProcessRepoMock
            .Setup(r => r.AreRunningProcessesExistAsync())
            .ReturnsAsync(false);

        _serviceProviderMock = new Mock<IServiceProvider>();
        _scopeMock = new Mock<IServiceScope>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();

        // Setup scope factory chain
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);

        // Setup service provider to return mocked repositories
        // GetRequiredService<T>() internally calls GetService(typeof(T)), so we mock that.
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ETLExecutionHistoryRepository)))
            .Returns(_historyRepoMock.Object);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(DtxProcessRepository)))
            .Returns(_dtxProcessRepoMock.Object);

        // Configuration with default daily codes
        var configData = new Dictionary<string, string?>
        {
            ["QueueConfig:DailyCodes:0"] = "DAILY001",
            ["QueueConfig:DailyCodes:1"] = "DAILY002",
            ["QueueConfig:MaxParallelExecutions"] = "2"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private EtlJobService CreateService()
    {
        return new EtlJobService(
            _scopeFactoryMock.Object,
            _executorMock.Object,
            _loggerMock.Object,
            _configuration,
            _notifierMock.Object);
    }

    private void SeedHistory(ETLExecutionHistory history)
    {
        _historyStore.Add(history);
        _historyRepoMock
            .Setup(r => r.GetByIdAsync(history.Id))
            .ReturnsAsync(history);
    }

    public void Dispose()
    {
        // No in-memory DB to clean up
    }

    // --- ETL Success + dtx_seguimiento verification passes ---

    [Fact]
    public async Task ExecuteJobByIdAsync_EtlSucceeds_AndDtxVerificationPasses_ShouldMarkExitoso()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV001",
            Codigo = "COD001",
            TipoEntidad = "ENTIDAD1",
            FechaDatos = new DateOnly(2024, 1, 15),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        // dtx_seguimiento verification returns matching period (January 2024)
        _historyRepoMock
            .Setup(r => r.VerifyDtxSeguimientoAsync("ENV001", "COD001"))
            .ReturnsAsync(new DtxSeguimientoVerificationResult
            {
                CodEnvio = "ENV001",
                FechaDatos = new DateOnly(2024, 1, 20)
            });

        _historyRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _historyRepoMock
            .Setup(r => r.UpdateStatusWithFechaDatosAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ReturnsAsync(new CommandExecutor.ExecutionResult
            {
                Success = true,
                ExitCode = 0,
                Output = "ETL completed",
                Error = string.Empty
            });

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - verify UpdateStatusWithFechaDatosAsync was called with "EXITOSO"
        _historyRepoMock.Verify(
            r => r.UpdateStatusWithFechaDatosAsync(
                historyId, "EXITOSO", It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once,
            "Should mark as EXITOSO when ETL succeeds and dtx verification passes");
    }

    // --- ETL Success + dtx_seguimiento verification returns null ---

    [Fact]
    public async Task ExecuteJobByIdAsync_EtlSucceeds_AndDtxVerificationReturnsNull_ShouldMarkFallido()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV002",
            Codigo = "COD002",
            TipoEntidad = "ENTIDAD2",
            FechaDatos = new DateOnly(2024, 2, 1),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        // No dtx_seguimiento record for this cod_envio + codigo
        _historyRepoMock
            .Setup(r => r.VerifyDtxSeguimientoAsync("ENV002", "COD002"))
            .ReturnsAsync((DtxSeguimientoVerificationResult?)null);

        _historyRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _historyRepoMock
            .Setup(r => r.UpdateStatusWithFechaDatosAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ReturnsAsync(new CommandExecutor.ExecutionResult
            {
                Success = true,
                ExitCode = 0,
                Output = "ETL completed",
                Error = string.Empty
            });

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - should mark as FALLIDO with descriptive error
        _historyRepoMock.Verify(
            r => r.UpdateStatusWithFechaDatosAsync(
                historyId, "FALLIDO", It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.Is<string>(s => s != null && s.Contains("No se encontraron datos")),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once,
            "Should mark as FALLIDO when dtx verification returns null");
    }

    // --- ETL Success + dtx_seguimiento verification period mismatch ---

    [Fact]
    public async Task ExecuteJobByIdAsync_EtlSucceeds_AndDtxVerificationFailsPeriod_ShouldMarkFallido()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV003",
            Codigo = "COD003",
            TipoEntidad = "ENTIDAD3",
            FechaDatos = new DateOnly(2024, 3, 15),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        // dtx_seguimiento returns February instead of March (period mismatch)
        _historyRepoMock
            .Setup(r => r.VerifyDtxSeguimientoAsync("ENV003", "COD003"))
            .ReturnsAsync(new DtxSeguimientoVerificationResult
            {
                CodEnvio = "ENV003",
                FechaDatos = new DateOnly(2024, 2, 20)
            });

        _historyRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _historyRepoMock
            .Setup(r => r.UpdateStatusWithFechaDatosAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ReturnsAsync(new CommandExecutor.ExecutionResult
            {
                Success = true,
                ExitCode = 0,
                Output = "ETL completed",
                Error = string.Empty
            });

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - should mark as FALLIDO because period doesn't match
        _historyRepoMock.Verify(
            r => r.UpdateStatusWithFechaDatosAsync(
                historyId, "FALLIDO", It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.Is<string>(s => s != null && s.Contains("No se encontraron datos")),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once,
            "Should mark as FALLIDO when dtx verification period doesn't match");
    }

    // --- ETL Failure: non-zero exit code - should verify dtx_seguimiento and update with FechaDatos ---

    [Fact]
    public async Task ExecuteJobByIdAsync_EtlFailsWithNonZeroExitCode_ShouldVerifyDtxAndUpdateWithFechaDatos()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV004",
            Codigo = "COD004",
            TipoEntidad = "ENTIDAD4",
            FechaDatos = new DateOnly(2024, 4, 1),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        // dtx_seguimiento verification still runs on failure
        _historyRepoMock
            .Setup(r => r.VerifyDtxSeguimientoAsync("ENV004", "COD004"))
            .ReturnsAsync(new DtxSeguimientoVerificationResult
            {
                CodEnvio = "ENV004",
                FechaDatos = new DateOnly(2024, 4, 10)
            });

        _historyRepoMock
            .Setup(r => r.UpdateStatusWithFechaDatosAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ReturnsAsync(new CommandExecutor.ExecutionResult
            {
                Success = false,
                ExitCode = 1,
                Output = "Partial output",
                Error = "Connection refused"
            });

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - should call VerifyDtxSeguimientoAsync
        _historyRepoMock.Verify(
            r => r.VerifyDtxSeguimientoAsync("ENV004", "COD004"),
            Times.Once,
            "Should call dtx verification when ETL fails");

        // Assert - should mark as FALLIDO with FechaDatos from verification
        _historyRepoMock.Verify(
            r => r.UpdateStatusWithFechaDatosAsync(
                historyId, "FALLIDO", new DateOnly(2024, 4, 10),
                1, "Partial output", "Connection refused",
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once,
            "Should mark as FALLIDO with FechaDatos from dtx verification");
    }

    // --- ETL Failure: dtx_seguimiento returns null - should still update with null FechaDatos ---

    [Fact]
    public async Task ExecuteJobByIdAsync_EtlFails_AndDtxVerificationReturnsNull_ShouldMarkFallidoWithNullFechaDatos()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV005",
            Codigo = "COD005",
            TipoEntidad = "ENTIDAD5",
            FechaDatos = new DateOnly(2024, 5, 1),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        // dtx_seguimiento returns null (no record found)
        _historyRepoMock
            .Setup(r => r.VerifyDtxSeguimientoAsync("ENV005", "COD005"))
            .ReturnsAsync((DtxSeguimientoVerificationResult?)null);

        _historyRepoMock
            .Setup(r => r.UpdateStatusWithFechaDatosAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ReturnsAsync(new CommandExecutor.ExecutionResult
            {
                Success = false,
                ExitCode = 3,
                Output = string.Empty,
                Error = "Timeout waiting for data"
            });

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - should call VerifyDtxSeguimientoAsync
        _historyRepoMock.Verify(
            r => r.VerifyDtxSeguimientoAsync("ENV005", "COD005"),
            Times.Once,
            "Should call dtx verification when ETL fails");

        // Assert - should mark as FALLIDO with null FechaDatos
        _historyRepoMock.Verify(
            r => r.UpdateStatusWithFechaDatosAsync(
                historyId, "FALLIDO", It.Is<DateOnly?>(d => !d.HasValue),
                3, string.Empty, "Timeout waiting for data",
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once,
            "Should mark as FALLIDO with null FechaDatos when dtx verification returns null");
    }

    // --- ETL throws exception ---

    [Fact]
    public async Task ExecuteJobByIdAsync_EtlThrowsException_ShouldMarkFallidoWithExceptionMessage()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV006",
            Codigo = "COD006",
            TipoEntidad = "ENTIDAD6",
            FechaDatos = new DateOnly(2024, 6, 1),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        _historyRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ThrowsAsync(new InvalidOperationException("Process not found"));

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - should mark as FALLIDO with exception message
        _historyRepoMock.Verify(
            r => r.UpdateStatusAsync(
                historyId, "FALLIDO", It.IsAny<int?>(), It.IsAny<string>(),
                It.Is<string>(s => s != null && s.Contains("Process not found")),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once,
            "Should mark as FALLIDO with exception message");

        _historyRepoMock.Verify(
            r => r.VerifyDtxSeguimientoAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Should NOT call dtx verification when ETL throws");
    }

    // --- History not found ---

    [Fact]
    public async Task ExecuteJobByIdAsync_HistoryNotFound_ShouldReturnWithoutUpdatingStatus()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        // Return null for non-existent history
        _historyRepoMock
            .Setup(r => r.GetByIdAsync(historyId))
            .ReturnsAsync((ETLExecutionHistory?)null);

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - no status update should occur
        _historyRepoMock.Verify(
            r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Never,
            "Should not update status when history not found");

        _historyRepoMock.Verify(
            r => r.VerifyDtxSeguimientoAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Should not verify dtx when history not found");
    }

    // --- Day-based code with matching period ---

    [Fact]
    public async Task ExecuteJobByIdAsync_DailyCode_EtlSucceeds_AndDtxMatchesDay_ShouldMarkExitoso()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV001",
            Codigo = "DAILY001",  // This is in the daily codes list
            TipoEntidad = "ENTIDAD1",
            FechaDatos = new DateOnly(2024, 3, 15),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        // dtx_seguimiento returns the exact same day (2024-03-15 is within [2024-03-15, 2024-03-16])
        _historyRepoMock
            .Setup(r => r.VerifyDtxSeguimientoAsync("ENV001", "DAILY001"))
            .ReturnsAsync(new DtxSeguimientoVerificationResult
            {
                CodEnvio = "ENV001",
                FechaDatos = new DateOnly(2024, 3, 15)
            });

        _historyRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _historyRepoMock
            .Setup(r => r.UpdateStatusWithFechaDatosAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ReturnsAsync(new CommandExecutor.ExecutionResult
            {
                Success = true,
                ExitCode = 0,
                Output = "ETL completed",
                Error = string.Empty
            });

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert
        _historyRepoMock.Verify(
            r => r.UpdateStatusWithFechaDatosAsync(
                historyId, "EXITOSO", It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once,
            "Should mark as EXITOSO when daily code matches dtx verification");
    }

    [Fact]
    public async Task ExecuteJobByIdAsync_DailyCode_EtlFails_ShouldVerifyDtxAndUpdateWithFechaDatos()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV001",
            Codigo = "DAILY001",
            TipoEntidad = "ENTIDAD1",
            FechaDatos = new DateOnly(2024, 3, 15),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        // dtx_seguimiento verification still runs on failure for daily codes too
        _historyRepoMock
            .Setup(r => r.VerifyDtxSeguimientoAsync("ENV001", "DAILY001"))
            .ReturnsAsync(new DtxSeguimientoVerificationResult
            {
                CodEnvio = "ENV001",
                FechaDatos = new DateOnly(2024, 3, 14)
            });

        _historyRepoMock
            .Setup(r => r.UpdateStatusWithFechaDatosAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ReturnsAsync(new CommandExecutor.ExecutionResult
            {
                Success = false,
                ExitCode = 3,
                Output = string.Empty,
                Error = "Timeout waiting for data"
            });

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - should call VerifyDtxSeguimientoAsync
        _historyRepoMock.Verify(
            r => r.VerifyDtxSeguimientoAsync("ENV001", "DAILY001"),
            Times.Once,
            "Should call dtx verification when ETL fails for daily code");

        // Assert - should mark as FALLIDO with FechaDatos from verification
        _historyRepoMock.Verify(
            r => r.UpdateStatusWithFechaDatosAsync(
                historyId, "FALLIDO", new DateOnly(2024, 3, 14),
                3, string.Empty, "Timeout waiting for data",
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once,
            "Should mark as FALLIDO with FechaDatos from dtx verification for daily code");
    }

    // --- ETL Failure with specific FechaDatos value verification ---

    [Fact]
    public async Task ExecuteJobByIdAsync_EtlFails_ShouldSetFechaDatosToLastDtxSeguimientoRecord()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var history = new ETLExecutionHistory
        {
            Id = historyId,
            CodEnvio = "ENV010",
            Codigo = "COD010",
            TipoEntidad = "ENTIDAD10",
            FechaDatos = new DateOnly(2024, 7, 1),
            Status = "PENDIENTE",
            TriggerType = "MANUAL"
        };
        SeedHistory(history);

        // dtx_seguimiento returns a specific FechaDatos
        var expectedFechaDatos = new DateOnly(2024, 7, 25);
        _historyRepoMock
            .Setup(r => r.VerifyDtxSeguimientoAsync("ENV010", "COD010"))
            .ReturnsAsync(new DtxSeguimientoVerificationResult
            {
                CodEnvio = "ENV010",
                FechaDatos = expectedFechaDatos
            });

        _historyRepoMock
            .Setup(r => r.UpdateStatusWithFechaDatosAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.ExecuteAsync(It.IsAny<ExecutionQueueItem>()))
            .ReturnsAsync(new CommandExecutor.ExecutionResult
            {
                Success = false,
                ExitCode = 2,
                Output = "Some partial work done",
                Error = "Data source unavailable"
            });

        var service = CreateService();

        // Act
        await service.ExecuteJobByIdAsync(historyId, "MANUAL");

        // Assert - FechaDatos must match the last dtx_seguimiento record for same cod_envio + codigo
        _historyRepoMock.Verify(
            r => r.UpdateStatusWithFechaDatosAsync(
                historyId,
                "FALLIDO",
                expectedFechaDatos,  // This is the critical assertion
                2,
                "Some partial work done",
                "Data source unavailable",
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()),
            Times.Once,
            "Should set FechaDatos to the last dtx_seguimiento record when ETL fails");
    }
}
