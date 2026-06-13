using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ServicioRESTEjecucionComandos.DTOs;
using System.Net.Http.Json;
using Xunit;

namespace ServicioRESTEjecucionComandos.IntegrationTests;

/// <summary>
/// WebApplicationFactory with rate limiting enabled for testing rate limit behavior.
/// Configures a low permit limit so tests can verify 429 responses.
/// </summary>
public class RateLimitingTestWebApplicationFactory : IntegrationTestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Override rate limiting to allow only 3 requests per window for testing.
        builder.UseSetting("RateLimiting:LoginPolicy:MaxRequests", "3");
        builder.UseSetting("RateLimiting:LoginPolicy:WindowSeconds", "60");
    }
}

/// <summary>
/// Integration tests for rate limiting on the login endpoint.
/// Uses a single test to avoid shared rate limiter state across multiple test methods.
/// </summary>
public class RateLimitingIntegrationTests : IClassFixture<RateLimitingTestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly RateLimitingTestWebApplicationFactory _factory;

    public RateLimitingIntegrationTests(RateLimitingTestWebApplicationFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabasesCreatedAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_RateLimiting_PreventsExcessiveRequests()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Test123!"
        };

        // Act - Send 5 requests sequentially. First 3 should succeed, remaining should be rate limited.
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 5; i++)
        {
            responses.Add(await _client.PostAsJsonAsync("/api/auth/login", loginRequest));
        }

        // Assert - At least one successful response before rate limiting kicks in.
        var successfulResponses = responses.Count(r => r.IsSuccessStatusCode);
        successfulResponses.Should().BeGreaterThan(0, "some requests should succeed before rate limit is reached");

        // Assert - At least one 429 Too Many Requests response.
        var tooManyRequestsCount = responses.Count(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests);
        tooManyRequestsCount.Should().BeGreaterThan(0, "rate limiting should reject excess requests");

        // Assert - The 429 response contains proper JSON error message.
        var rateLimitedResponse = responses.First(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests);
        var errorContent = await rateLimitedResponse.Content.ReadAsStringAsync();
        errorContent.Should().Contain("Demasiadas Peticiones");

        // Assert - The 429 response has correct content type.
        rateLimitedResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
