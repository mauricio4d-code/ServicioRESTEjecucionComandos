using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ServicioRESTEjecucionComandos.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace ServicioRESTEjecucionComandos.IntegrationTests;

/// <summary>
/// Integration tests for SchedulesController endpoints.
/// </summary>
public class SchedulesControllerIntegrationTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestWebApplicationFactory _factory;

    public SchedulesControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabasesCreatedAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllSchedules_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/schedules");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetScheduleById_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/schedules/some-guid");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSchedule_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var createDto = new CreateEtlScheduleDto
        {
            Params = "--test-param",
            CronExpression = "0 0 * * *"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/schedules", createDto);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSchedule_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var updateDto = new UpdateEtlScheduleDto
        {
            Params = "--updated-param",
            CronExpression = "0 12 * * *",
            IsActive = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/schedules/some-guid", updateDto);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ToggleScheduleActive_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), "/api/schedules/some-guid/toggle");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteSchedule_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.DeleteAsync("/api/schedules/some-guid");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
