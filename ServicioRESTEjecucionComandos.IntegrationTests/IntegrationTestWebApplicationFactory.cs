using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration tests that replaces production DB contexts
/// with in-memory SQLite databases by overriding configuration values.
/// </summary>
public class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private static bool _initialized;
    // Keep connections open to preserve in-memory SQLite data across scopes.
    // Named in-memory databases (file:name?mode=memory&cache=shared) persist across connections
    // ONLY while at least one connection remains open.
    private static readonly List<SqliteConnection> _openConnections = new();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var conn in _openConnections)
            {
                conn.Dispose();
            }
            _openConnections.Clear();
            _initialized = false;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set test-specific configuration values BEFORE the app builds.
        // Program.cs reads these values to configure DbContext providers.
        builder.UseSetting("Authentication:Provider", "sqlite");
        builder.UseSetting("ServiceDb:Provider", "sqlite");
        builder.UseSetting("Jwt:SecretKey", "TestSecretKeyForIntegrationTestsThatIsLongEnough1234567890");
        builder.UseSetting("Jwt:Issuer", "TestIssuer");
        builder.UseSetting("Jwt:Audience", "TestAudience");
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenMinutes", "1440");
        // Use named in-memory SQLite databases so all connections share the same database.
        // The "file:name?mode=memory&cache=shared" URI format creates a named in-memory database
        // that persists across multiple connections (unlike ":memory:" which is connection-local).
        builder.UseSetting("ConnectionStrings:AuthDatabase", "Data Source=file:auth_test?mode=memory&cache=shared");
        builder.UseSetting("ConnectionStrings:RefreshTokenDatabase", "Data Source=file:refreshtoken_test?mode=memory&cache=shared");
        builder.UseSetting("ConnectionStrings:ServiceDatabase", "Data Source=file:service_test?mode=memory&cache=shared");
    }

    /// <summary>
    /// Ensures all in-memory databases are created and seeded before tests run.
    /// </summary>
    public async Task EnsureDatabasesCreatedAsync()
    {
        await _initSemaphore.WaitAsync();
        if (_initialized)
        {
            _initSemaphore.Release();
            return;
        }

        try
        {
            using var scope = Services.CreateScope();
            var services = scope.ServiceProvider;

            // Create AuthDbContext database and seed test user.
            // Keep the connection open so the named in-memory database persists.
            using var authContext = services.GetRequiredService<AuthDbContext>();
            var authConnection = (SqliteConnection)authContext.Database.GetDbConnection();
            await authConnection.OpenAsync();
            _openConnections.Add(authConnection);
            await authContext.Database.EnsureCreatedAsync();

            // Seed test user role if not exists
            if (!await authContext.UserRoles.AnyAsync())
            {
                await authContext.UserRoles.AddAsync(new UserRole { Name = "Administrador" });
                await authContext.UserRoles.AddAsync(new UserRole { Name = "Usuario" });
                await authContext.SaveChangesAsync();
            }

            // Seed test user if not exists (password "Test123!" MD5 hashed)
            if (!await authContext.Users.AnyAsync(u => u.Email == "test@example.com"))
            {
                var adminRole = await authContext.UserRoles.FirstAsync(r => r.Name == "Administrador");
                // MD5 hash of "Test123!"
                var passwordHash = System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes("Test123!"));
                var passwordHashHex = Convert.ToHexString(passwordHash).ToLowerInvariant();

                await authContext.Users.AddAsync(new User
                {
                    Name = "Test",
                    Email = "test@example.com",
                    Password = passwordHashHex,
                    Userstate = "Activo",
                    Userroleid = adminRole.Id,
                    Firstname = "Test",
                    Lastname = "User",
                    Description = "Test user for integration tests"
                });
                await authContext.SaveChangesAsync();
            }

            // Create RefreshTokenDbContext database
            using var refreshTokenContext = services.GetRequiredService<RefreshTokenDbContext>();
            var rtConnection = (SqliteConnection)refreshTokenContext.Database.GetDbConnection();
            await rtConnection.OpenAsync();
            _openConnections.Add(rtConnection);
            await refreshTokenContext.Database.EnsureCreatedAsync();

            // Create ScheduleDbContext database and ensure etl_schedule table exists
            using var scheduleContext = services.GetRequiredService<ScheduleDbContext>();
            var scheduleConnection = (SqliteConnection)scheduleContext.Database.GetDbConnection();
            await scheduleConnection.OpenAsync();
            _openConnections.Add(scheduleConnection);
            await scheduleContext.Database.EnsureCreatedAsync();

            // Create ServiceDbContext tables using raw SQL (SQLite-compatible).
            // Program.cs creates these on startup, but we need them in the same scope as our tests.
            using var serviceContext = services.GetRequiredService<ServiceDbContext>();
            var serviceConnection = (SqliteConnection)serviceContext.Database.GetDbConnection();
            await serviceConnection.OpenAsync();
            _openConnections.Add(serviceConnection);

            // Create hist_etl_execution table (SQLite syntax)
            serviceContext.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""hist_etl_execution"" (
                    ""Id"" TEXT PRIMARY KEY,
                    ""CodEnvio"" TEXT NOT NULL,
                    ""TipoEntidad"" TEXT NOT NULL,
                    ""FechaDatos"" TEXT NOT NULL,
                    ""Codigo"" TEXT NOT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT 'PENDIENTE',
                    ""TriggerType"" TEXT NOT NULL DEFAULT 'MANUAL',
                    ""ExitCode"" INTEGER,
                    ""Output"" TEXT,
                    ""Error"" TEXT,
                    ""ExecutedAt"" TEXT,
                    ""CompletedAt"" TEXT
                );
            ");

            serviceContext.Database.ExecuteSqlRaw(@"
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_Status"" ON ""hist_etl_execution"" (""Status"");
            ");

            // Create hist_etl_execution_scheduled table (SQLite syntax)
            serviceContext.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""hist_etl_execution_scheduled"" (
                    ""Id"" TEXT PRIMARY KEY,
                    ""ScheduleId"" TEXT NOT NULL,
                    ""Params"" TEXT,
                    ""Status"" TEXT NOT NULL DEFAULT 'PENDIENTE',
                    ""ExitCode"" INTEGER,
                    ""Output"" TEXT,
                    ""Error"" TEXT,
                    ""ExecutedAt"" TEXT,
                    ""CompletedAt"" TEXT,
                    ""CreatedAt"" TEXT NOT NULL DEFAULT (datetime('now'))
                );
            ");

            serviceContext.Database.ExecuteSqlRaw(@"
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_scheduled_ScheduleId"" ON ""hist_etl_execution_scheduled"" (""ScheduleId"");
            ");

            serviceContext.Database.ExecuteSqlRaw(@"
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_scheduled_Status"" ON ""hist_etl_execution_scheduled"" (""Status"");
            ");

            _initialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

}

/// <summary>
/// Marker interface for integration test classes that require database setup.
/// </summary>
public interface IIntegrationTestFixture
{
    Task InitializeAsync();
}
