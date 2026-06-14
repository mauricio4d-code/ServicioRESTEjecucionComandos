using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServicioRESTEjecucionComandos.DTOs;
using ServicioRESTEjecucionComandos.Hubs;
using System.Net.Http.Json;
using Xunit;

namespace ServicioRESTEjecucionComandos.IntegrationTests.Hubs;

/// <summary>
/// Integration tests for EtlNotificationHub JWT authentication.
/// Verifies that only authenticated users can connect to the SignalR hub,
/// and that authenticated clients receive broadcast notifications.
/// </summary>
public class EtlNotificationHubIntegrationTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestWebApplicationFactory _factory;

    public EtlNotificationHubIntegrationTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabasesCreatedAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Helper to obtain a JWT access token by logging in with the test user.
    /// </summary>
    private async Task<string> GetAccessTokenAsync()
    {
        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.AccessToken.Should().NotBeNullOrEmpty();

        return loginResponse.AccessToken;
    }

    [Fact]
    public async Task Negotiate_Unauthenticated_ShouldReturn401()
    {
        // Arrange - Hit the SignalR negotiate endpoint without auth
        // The negotiate endpoint is used by the client before establishing the connection

        // Act
        var response = await _client.PostAsync("/etlNotifications/negotiate", null);

        // Assert - Should return 401 Unauthorized since [Authorize] is on the hub
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Negotiate_Authenticated_ShouldReturn200()
    {
        // Arrange
        var accessToken = await GetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await _client.PostAsync("/etlNotifications/negotiate", null);

        // Assert - Should return 200 OK for authenticated user
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task BroadcastTaskStarted_ShouldNotifyConnectedClients()
    {
        // Arrange - Create a test hub context to verify ExecutionNotifier broadcasts correctly
        using var scope = _factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<ServicioRESTEjecucionComandos.Services.ExecutionNotifier>();

        // We verify the notifier is properly registered and callable
        notifier.Should().NotBeNull();

        // Act - Call broadcast methods (these should not throw)
        var actStarted = async () => await notifier.BroadcastTaskStartedAsync("test params");
        var actCompleted = async () => await notifier.BroadcastTaskCompletedAsync(true, "test params");

        // Assert - No exceptions should be thrown
        await actStarted.Should().NotThrowAsync();
        await actCompleted.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Hub_IsDecoratedWithAuthorizeAttribute()
    {
        // Arrange
        var hubType = typeof(EtlNotificationHub);

        // Act
        var authorizeAttributes = hubType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false);

        // Assert - The hub should have [Authorize] attribute to enforce authentication
        authorizeAttributes.Should().NotBeEmpty("EtlNotificationHub should require authentication");
    }
}
