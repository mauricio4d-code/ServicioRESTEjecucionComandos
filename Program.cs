using System.Reflection;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ServicioRESTEjecucionComandos.Constants;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Interfaces;
using ServicioRESTEjecucionComandos.Repositories;
using ServicioRESTEjecucionComandos.Hubs;
using ServicioRESTEjecucionComandos.Services;
using ServicioRESTEjecucionComandos.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// Production: Ensure ProgramData directory exists for SQLite DB and Logs
// Only applies when ASPNETCORE_ENVIRONMENT=Production
// -----------------------------------------------------------------------
if (builder.Environment.IsProduction())
{
    var serviceDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ServicioRESTEjecucionComandos");
    var serviceLogsDir = Path.Combine(serviceDataDir, "Logs");

    if (!Directory.Exists(serviceDataDir))
    {
        Directory.CreateDirectory(serviceDataDir);
    }
    if (!Directory.Exists(serviceLogsDir))
    {
        Directory.CreateDirectory(serviceLogsDir);
    }

    var sqliteDbPath = Path.Combine(serviceDataDir, "ServicioRESTEjecucionComandos.db");
    var sqliteConnectionString = $"Data Source={sqliteDbPath}";

    // Override RefreshTokenDatabase connection string to use absolute ProgramData path
    // Skip override when running under test (in-memory SQLite databases)
    var existingRefreshTokenConnectionString = builder.Configuration.GetConnectionString("RefreshTokenDatabase");
    if (string.IsNullOrEmpty(existingRefreshTokenConnectionString) || !existingRefreshTokenConnectionString.Contains("mode=memory"))
    {
        builder.Configuration.GetSection("ConnectionStrings")["RefreshTokenDatabase"] = sqliteConnectionString;
    }

    // Override Serilog log file path to use ProgramData directory
    builder.Configuration["Serilog:WriteTo:1:Args:path"] = Path.Combine(serviceLogsDir, "log-.txt");
}

// -----------------------------------------------------------------------
// Windows Service support (dual-mode: works as console and Windows Service)
// -----------------------------------------------------------------------
builder.Host.UseWindowsService();

// -----------------------------------------------------------------------
// Serilog configuration (reads from appsettings.json Serilog section)
// -----------------------------------------------------------------------
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Read configuration values
var dataxConfig = builder.Configuration.GetSection("DataxConfig");
var queueConfig = builder.Configuration.GetSection("QueueConfig");
var authenticationConfig = builder.Configuration.GetSection("Authentication");
var serviceDbConfig = builder.Configuration.GetSection("ServiceDb");

var exePath = dataxConfig.GetValue<string>("ExePath") ?? string.Empty;

var waitSeconds = queueConfig.GetValue<int>("WaitSeconds");
var maxParallelExecutions = queueConfig.GetValue<int>("MaxParallelExecutions");

var authenticationProvider = authenticationConfig.GetValue<string>("Provider") ?? string.Empty;
var serviceDbProvider = serviceDbConfig.GetValue<string>("Provider") ?? "postgres";

// -----------------------------------------------------------------------
// EF Core DbContext registrations
// -----------------------------------------------------------------------

// Legacy Auth database (PostgreSQL / SQLServer / SQLite - configurable via ConnectionStrings:AuthDatabase)
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    var authConnectionString = builder.Configuration.GetConnectionString("AuthDatabase")
        ?? throw new InvalidOperationException("Connection string 'AuthDatabase' is not configured.");

    switch (authenticationProvider.ToLower())
    {
        case DbProvider.Postgres:
        case DbProvider.PostgreSQL:
            options.UseNpgsql(authConnectionString);
            break;
        case DbProvider.SqlServer:
            options.UseSqlServer(authConnectionString);
            break;
        case DbProvider.Sqlite:
        default:
            options.UseSqlite(authConnectionString);
            break;
    }
});

// Refresh Token + Audit Log database (SQLite - auto-created on startup)
builder.Services.AddDbContext<RefreshTokenDbContext>(options =>
{
    var rtConnectionString = builder.Configuration.GetConnectionString("RefreshTokenDatabase")
        ?? throw new InvalidOperationException("Connection string 'RefreshTokenDatabase' is not configured.");

    options.UseSqlite(rtConnectionString);
});

// Service database (PostgreSQL / SQLServer - configurable via ServiceDb:Provider)
builder.Services.AddDbContext<ServiceDbContext>(options =>
{
    var serviceConnectionString = builder.Configuration.GetConnectionString("ServiceDatabase")
        ?? throw new InvalidOperationException("Connection string 'ServiceDatabase' is not configured.");

    switch (serviceDbProvider.ToLower())
    {
        case DbProvider.Postgres:
        case DbProvider.PostgreSQL:
            options.UseNpgsql(serviceConnectionString);
            break;
        case DbProvider.SqlServer:
            options.UseSqlServer(serviceConnectionString);
            break;
        case DbProvider.Sqlite: // NO NOT ADD THIS TO README, this is only for testing and local development convenience, not intended for production use.
            options.UseSqlite(serviceConnectionString);
            break;
        default:
            options.UseNpgsql(serviceConnectionString);
            break;
    }
});

// ScheduleDbContext (SQLite - auto-created on startup for etl_schedule table)
builder.Services.AddDbContext<ScheduleDbContext>(options =>
{
    var scheduleConnectionString = builder.Configuration.GetConnectionString("RefreshTokenDatabase")
        ?? throw new InvalidOperationException("Connection string 'RefreshTokenDatabase' is not configured.");

    options.UseSqlite(scheduleConnectionString);
});

// -----------------------------------------------------------------------
// Repository registrations
// -----------------------------------------------------------------------
builder.Services.AddScoped<RefreshTokenRepository>();
builder.Services.AddScoped<AuthAuditLogRepository>();
builder.Services.AddScoped<ETLExecutionHistoryRepository>();
builder.Services.AddScoped<EtlScheduleRepository>();
builder.Services.AddScoped<ETLExecutionHistoryScheduledRepository>();
builder.Services.AddScoped<DtxProcessRepository>();

// -----------------------------------------------------------------------
// Service registrations
// -----------------------------------------------------------------------
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddSingleton<IPasswordValidator, LegacyPasswordValidator>();

// -----------------------------------------------------------------------
// Existing service registrations
// -----------------------------------------------------------------------

// Add controllers
builder.Services.AddControllers();

// -----------------------------------------------------------------------
// Rate Limiting configuration (protects login endpoint from brute-force)
// -----------------------------------------------------------------------
var rateLimitingConfig = builder.Configuration.GetSection("RateLimiting:LoginPolicy");
builder.Services.AddRateLimiter(options =>
{
    var windowSeconds = rateLimitingConfig.GetValue<int>("WindowSeconds", 60);
    var maxRequests = rateLimitingConfig.GetValue<int>("MaxRequests", 5);

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"Demasiadas Peticiones\",\"message\":\"Se han realizado demasiados intentos de inicio de sesión. Inténtelo de nuevo más tarde.\"}",
            token);
    };

    options.AddFixedWindowLimiter(policyName: "LoginPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = maxRequests;
        limiterOptions.Window = TimeSpan.FromSeconds(windowSeconds);
        limiterOptions.QueueLimit = 0;
    });
});

// Register CommandExecutor as singleton (parameters are now per-item, not static)
builder.Services.AddSingleton<CommandExecutor>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CommandExecutor>>();
    return new CommandExecutor(exePath, logger);
});

// Register EtlJobService as singleton (Hangfire jobs require singleton resolution)
builder.Services.AddSingleton<EtlJobService>();

// Register RefreshTokenCleanupService as hosted service (background cleanup)
builder.Services.AddHostedService<RefreshTokenCleanupService>();

// Register ScheduleSyncService as hosted service (syncs etl_schedule with Hangfire recurring jobs)
builder.Services.AddHostedService<ScheduleSyncService>();

// Register ServiceRestartMonitorService as hosted service (polls reiniciar_servicio and restarts Windows service)
builder.Services.AddHostedService<ServiceRestartMonitorService>();

// -----------------------------------------------------------------------
// SignalR configuration (real-time notifications for scheduled ETL tasks)
// -----------------------------------------------------------------------
builder.Services.AddSignalR();

// Register ExecutionNotifier as singleton (broadcasts to SignalR clients)
builder.Services.AddSingleton<ExecutionNotifier>();

// -----------------------------------------------------------------------
// Health Checks registration
// -----------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<AuthDbHealthCheck>("auth_database", tags: new[] { "database" })
    .AddCheck<ServiceDbHealthCheck>("service_database", tags: new[] { "database" })
    .AddCheck<SqliteDbHealthCheck>("sqlite_database", tags: new[] { "database" })
    .AddCheck<HangfireHealthCheck>("hangfire", tags: new[] { "background-jobs" })
    .AddCheck<UptimeHealthCheck>("uptime", tags: new[] { "system" });

// -----------------------------------------------------------------------
// Hangfire configuration (no dashboard, uses existing SQLite database)
// -----------------------------------------------------------------------
builder.Services.AddHangfire(config =>
{
    config.UseInMemoryStorage();
});

// Register Hangfire server (background worker) - dashboard is NOT enabled
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 5;
    options.Queues = new[] { "default" };
    options.ShutdownTimeout = TimeSpan.FromMinutes(1);
});

// -----------------------------------------------------------------------
// JWT Authentication configuration
// -----------------------------------------------------------------------
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT Audience is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };

    // Allow extraction of token from Authorization header
    options.Events = new JwtBearerEvents
    {
        // Extract token from query string for SignalR negotiate requests
        // SignalR client's withAccessTokenFactory() sends token as "access_token" query param
        OnMessageReceived = context =>
        {
            if (context.Request.Path.StartsWithSegments("/etlNotifications")
                && context.Request.Query.ContainsKey("access_token"))
            {
                context.Token = context.Request.Query["access_token"];
            }
            return Task.CompletedTask;
        },

        OnChallenge = context =>
        {
            // Skip custom JSON response for SignalR negotiation requests
            // to allow SignalR client to receive proper 401 and handle disconnect
            if (context.Request.Path.StartsWithSegments("/etlNotifications"))
            {
                return Task.CompletedTask;
            }

            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Valid authentication token required.\"}");
        },
        OnForbidden = _ => Task.CompletedTask
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policy.AdminOnly, policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, Role.Administrador, Role.AdministadorTypo));
});

var app = builder.Build();

// -----------------------------------------------------------------------
// Ensure databases are created on startup
// -----------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Log Serilog file retention setting
    var logRetentionDays = builder.Configuration.GetValue<int>("Serilog:WriteTo:1:Args:retainedFileCountLimit", 0);
    if (logRetentionDays > 0)
    {
        logger.LogInformation("Log file retention: {Days} days (retainedFileCountLimit).", logRetentionDays);
    }

    try
    {
        var refreshTokenDbContext = services.GetRequiredService<RefreshTokenDbContext>();
        refreshTokenDbContext.Database.EnsureCreated();
        logger.LogInformation("RefreshToken SQLite database ensured (created if not exists).");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred creating the RefreshToken SQLite database.");
    }

    try
    {
        var scheduleDbContext = services.GetRequiredService<ScheduleDbContext>();

        // Use raw SQL because EnsureCreated() only runs when the DB file is new,
        // and this file already exists from RefreshTokenDbContext setup.
        scheduleDbContext.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""etl_schedule"" (
                ""Id"" TEXT PRIMARY KEY,
                ""Params"" TEXT,
                ""CronExpression"" TEXT NOT NULL,
                ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                ""CreatedAt"" TEXT NOT NULL,
                ""UpdatedAt"" TEXT
            );
        ");
        scheduleDbContext.Database.ExecuteSqlRaw(@"
            CREATE INDEX IF NOT EXISTS ""IX_etl_schedule_IsActive"" ON ""etl_schedule"" (""IsActive"");
        ");
        logger.LogInformation("Schedule SQLite database ensured (etl_schedule table created if not exists).");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred creating the Schedule SQLite database.");
    }

    try
    {
        var serviceDbContext = services.GetRequiredService<ServiceDbContext>();

        string createTableSql;
        string createIndexSql;

        if (serviceDbProvider.ToLower() == DbProvider.SqlServer)
        {
            // SQL Server syntax
            createTableSql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'hist_etl_execution')
                BEGIN
                    CREATE TABLE [hist_etl_execution] (
                        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        [CodEnvio] NVARCHAR(50) NOT NULL,
                        [TipoEntidad] NVARCHAR(50) NOT NULL,
                        [FechaDatos] DATE NOT NULL,
                        [Codigo] NVARCHAR(50) NOT NULL,
                        [Status] NVARCHAR(20) NOT NULL DEFAULT '{EtlStatus.Pending}',
                        [TriggerType] NVARCHAR(20) NOT NULL DEFAULT '{TriggerType.Manual}',
                        [ExitCode] INT,
                        [Output] NVARCHAR(MAX),
                        [Error] NVARCHAR(MAX),
                        [ExecutedAt] DATETIME2,
                        [CompletedAt] DATETIME2
                    );
                END";
            createIndexSql = @"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_hist_etl_execution_Status')
                    CREATE INDEX [IX_hist_etl_execution_Status] ON [hist_etl_execution] ([Status]);";
        }
        else if (serviceDbProvider.ToLower() == DbProvider.Sqlite)
        {
            // SQLite syntax
            createTableSql = $@"
                CREATE TABLE IF NOT EXISTS ""hist_etl_execution"" (
                    ""Id"" TEXT PRIMARY KEY,
                    ""CodEnvio"" TEXT NOT NULL,
                    ""TipoEntidad"" TEXT NOT NULL,
                    ""FechaDatos"" TEXT NOT NULL,
                    ""Codigo"" TEXT NOT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT '{EtlStatus.Pending}',
                    ""TriggerType"" TEXT NOT NULL DEFAULT '{TriggerType.Manual}',
                    ""ExitCode"" INTEGER,
                    ""Output"" TEXT,
                    ""Error"" TEXT,
                    ""ExecutedAt"" TEXT,
                    ""CompletedAt"" TEXT
                )";
            createIndexSql = @"
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_Status"" ON ""hist_etl_execution"" (""Status"")";
        }
        else
        {
            // PostgreSQL syntax (default)
            serviceDbContext.Database.ExecuteSqlRaw(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            createTableSql = $@"
                CREATE TABLE IF NOT EXISTS ""hist_etl_execution"" (
                    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    ""CodEnvio"" VARCHAR(50) NOT NULL,
                    ""TipoEntidad"" VARCHAR(50) NOT NULL,
                    ""FechaDatos"" DATE NOT NULL,
                    ""Codigo"" VARCHAR(50) NOT NULL,
                    ""Status"" VARCHAR(20) NOT NULL DEFAULT '{EtlStatus.Pending}',
                    ""TriggerType"" VARCHAR(20) NOT NULL DEFAULT '{TriggerType.Manual}',
                    ""ExitCode"" INTEGER,
                    ""Output"" TEXT,
                    ""Error"" TEXT,
                    ""ExecutedAt"" TIMESTAMP WITH TIME ZONE,
                    ""CompletedAt"" TIMESTAMP WITH TIME ZONE
                )";
            createIndexSql = @"
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_Status"" ON ""hist_etl_execution"" (""Status"")";
        }

        serviceDbContext.Database.ExecuteSqlRaw(createTableSql);
        serviceDbContext.Database.ExecuteSqlRaw(createIndexSql);

        // Create unique partial index to prevent duplicate active executions for the same CodEnvio+Codigo.
        // This eliminates the race condition in UpsertOrGetActiveAsync by enforcing uniqueness at the DB level.
        // SQLite does not support partial indexes, so it is skipped (catch-and-retry in the repository handles it).
        if (serviceDbProvider.ToLower() == DbProvider.SqlServer)
        {
            serviceDbContext.Database.ExecuteSqlRaw($@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_hist_etl_execution_unique_active')
                    CREATE UNIQUE INDEX [IX_hist_etl_execution_unique_active] ON [hist_etl_execution] ([CodEnvio], [Codigo])
                    WHERE [Status] IN ('{EtlStatus.Pending}', '{EtlStatus.InProgress}');");
        }
        else if (serviceDbProvider.ToLower() != DbProvider.Sqlite)
        {
            // PostgreSQL syntax (default)
            serviceDbContext.Database.ExecuteSqlRaw($@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_hist_etl_execution_unique_active""
                    ON ""hist_etl_execution"" (""CodEnvio"", ""Codigo"")
                    WHERE ""Status"" IN ('{EtlStatus.Pending}', '{EtlStatus.InProgress}');");
        }

        // Create hist_etl_execution_scheduled table
        string createScheduledTableSql;
        string createScheduledIndexSql;

        if (serviceDbProvider.ToLower() == DbProvider.SqlServer)
        {
            // SQL Server syntax
            createScheduledTableSql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'hist_etl_execution_scheduled')
                BEGIN
                    CREATE TABLE [hist_etl_execution_scheduled] (
                        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        [ScheduleId] UNIQUEIDENTIFIER NOT NULL,
                        [Params] NVARCHAR(MAX),
                        [Status] NVARCHAR(20) NOT NULL DEFAULT '{EtlStatus.Pending}',
                        [ExitCode] INT,
                        [Output] NVARCHAR(MAX),
                        [Error] NVARCHAR(MAX),
                        [ExecutedAt] DATETIME2,
                        [CompletedAt] DATETIME2,
                        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END";
            createScheduledIndexSql = @"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_hist_etl_execution_scheduled_ScheduleId')
                    CREATE INDEX [IX_hist_etl_execution_scheduled_ScheduleId] ON [hist_etl_execution_scheduled] ([ScheduleId]);
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_hist_etl_execution_scheduled_Status')
                    CREATE INDEX [IX_hist_etl_execution_scheduled_Status] ON [hist_etl_execution_scheduled] ([Status]);";
        }
        else if (serviceDbProvider.ToLower() == DbProvider.Sqlite)
        {
            // SQLite syntax
            createScheduledTableSql = $@"
                CREATE TABLE IF NOT EXISTS ""hist_etl_execution_scheduled"" (
                    ""Id"" TEXT PRIMARY KEY,
                    ""ScheduleId"" TEXT NOT NULL,
                    ""Params"" TEXT,
                    ""Status"" TEXT NOT NULL DEFAULT '{EtlStatus.Pending}',
                    ""ExitCode"" INTEGER,
                    ""Output"" TEXT,
                    ""Error"" TEXT,
                    ""ExecutedAt"" TEXT,
                    ""CompletedAt"" TEXT,
                    ""CreatedAt"" TEXT NOT NULL DEFAULT (datetime('now'))
                )";
            createScheduledIndexSql = @"
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_scheduled_ScheduleId"" ON ""hist_etl_execution_scheduled"" (""ScheduleId"");
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_scheduled_Status"" ON ""hist_etl_execution_scheduled"" (""Status"")";
        }
        else
        {
            // PostgreSQL syntax (default)
            createScheduledTableSql = $@"
                CREATE TABLE IF NOT EXISTS ""hist_etl_execution_scheduled"" (
                    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    ""ScheduleId"" UUID NOT NULL,
                    ""Params"" TEXT,
                    ""Status"" VARCHAR(20) NOT NULL DEFAULT '{EtlStatus.Pending}',
                    ""ExitCode"" INTEGER,
                    ""Output"" TEXT,
                    ""Error"" TEXT,
                    ""ExecutedAt"" TIMESTAMP WITH TIME ZONE,
                    ""CompletedAt"" TIMESTAMP WITH TIME ZONE,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                )";
            createScheduledIndexSql = @"
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_scheduled_ScheduleId"" ON ""hist_etl_execution_scheduled"" (""ScheduleId"");
                CREATE INDEX IF NOT EXISTS ""IX_hist_etl_execution_scheduled_Status"" ON ""hist_etl_execution_scheduled"" (""Status"")";
        }

        serviceDbContext.Database.ExecuteSqlRaw(createScheduledTableSql);
        serviceDbContext.Database.ExecuteSqlRaw(createScheduledIndexSql);
        logger.LogInformation("Service database schema ensured via raw SQL (hist_etl_execution and hist_etl_execution_scheduled tables created if not exists).");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred creating the Service database schema.");
    }

}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Serve static files (HTML, CSS, JS) from wwwroot
app.UseStaticFiles();

// Authentication + Authorization middleware must come BEFORE endpoint routing
app.UseAuthentication();
app.UseAuthorization();

// Rate limiting middleware must come BEFORE endpoint routing
app.UseRateLimiter();

// Redirect root path to index.html
app.MapGet("/", () => Results.Redirect("/index.html"));

// Map API controllers
app.MapControllers();

// Map SignalR hub for real-time ETL notifications
app.MapHub<EtlNotificationHub>("/etlNotifications");

// -----------------------------------------------------------------------
// Health Check endpoint (unauthenticated - for external monitoring)
// -----------------------------------------------------------------------
app.MapHealthChecks("/api/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var status = report.Status.ToString().ToLower();
        var components = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

        var response = new
        {
            Status = report.Status.ToString(),
            Timestamp = DateTime.UtcNow.ToString("o"),
            Components = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    Status = entry.Value.Status.ToString(),
                    Description = entry.Value.Description,
                    Data = entry.Value.Data
                })
        };

        await context.Response.WriteAsJsonAsync(response);
    }
});

// -----------------------------------------------------------------------
// Log the service URL(s) for easy access
// -----------------------------------------------------------------------
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();

// Read additional configuration values for startup summary
var dailyCodes = builder.Configuration.GetSection("QueueConfig:DailyCodes").Get<string[]>() ?? Array.Empty<string>();
var excludedCodes = builder.Configuration.GetSection("QueueConfig:ExcludedCodes").Get<string[]>() ?? Array.Empty<string>();
var accessTokenMinutes = builder.Configuration.GetValue<int>("Jwt:AccessTokenMinutes");
var refreshTokenDays = builder.Configuration.GetValue<int>("Jwt:RefreshTokenDays");
var cleanupIntervalMinutes = builder.Configuration.GetValue<int>("RefreshTokenCleanup:CleanupIntervalMinutes");
var auditLogRetentionDays = builder.Configuration.GetValue<int>("RefreshTokenCleanup:AuditLogRetentionDays");

var configuredUrl = app.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://localhost:5000";

// Parse the configured URL to extract host and port
var uri = new Uri(configuredUrl);
var host = uri.Host;
var port = uri.Port;
var scheme = uri.Scheme;

// When bound to 0.0.0.0, resolve actual local IPs so users know where to connect
IEnumerable<string> accessibleAddresses;
if (host == "0.0.0.0")
{
    var localIps = NetworkInterface.GetAllNetworkInterfaces()
        .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(ni => ni.GetIPProperties().UnicastAddresses
            .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(u => u.Address.ToString()))
        .Distinct()
        .OrderBy(ip => ip)
        .ToList();

    accessibleAddresses = localIps.Select(ip => $"{scheme}://{ip}:{port}");
}
else
{
    accessibleAddresses = new[] { configuredUrl };
}

// ServiceRestart configuration
var restartServiceName = builder.Configuration.GetValue<string>("ServiceRestart:WindowsServiceName");
var restartCheckInterval = builder.Configuration.GetValue<int>("ServiceRestart:CheckIntervalMinutes");
var restartRunningMinutesThreshold = builder.Configuration.GetValue<int>("ServiceRestart:RunningMinutesThreshold");
var processCheckWaitSeconds = builder.Configuration.GetValue<int>("QueueConfig:ProcessCheckWaitSeconds");
startupLogger.LogInformation("  ServiceRestart.ServiceName:             {Service}", string.IsNullOrEmpty(restartServiceName) ? "(not configured)" : restartServiceName);
startupLogger.LogInformation("  ServiceRestart.CheckInterval:           {Minutes} min", restartCheckInterval);
startupLogger.LogInformation("  ServiceRestart.RunningMinutesThreshold: {Minutes} min", restartRunningMinutesThreshold);
startupLogger.LogInformation("  QueueConfig.ProcessCheckWaitSeconds:    {Seconds} s", processCheckWaitSeconds);

startupLogger.LogInformation("============================================");
startupLogger.LogInformation("Configuration Summary:");
startupLogger.LogInformation("  QueueConfig.DailyCodes:        [{Codes}]", string.Join(", ", dailyCodes));
startupLogger.LogInformation("  QueueConfig.ExcludedCodes:     [{Codes}]", string.Join(", ", excludedCodes));
startupLogger.LogInformation("  ServiceDb.Provider:            {Provider}", serviceDbProvider);
startupLogger.LogInformation("  Authentication.Provider:       {Provider}", authenticationProvider);
startupLogger.LogInformation("  Jwt.AccessTokenMinutes:        {Minutes}", accessTokenMinutes);
startupLogger.LogInformation("  Jwt.RefreshTokenDays:          {Days}", refreshTokenDays);
startupLogger.LogInformation("  RefreshTokenCleanup.Interval:  {Minutes} min", cleanupIntervalMinutes);
startupLogger.LogInformation("  RefreshTokenCleanup.Retention: {Days} days", auditLogRetentionDays);

startupLogger.LogInformation("============================================");
startupLogger.LogInformation("ServicioRESTEjecucionComandos is running!");
startupLogger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
startupLogger.LogInformation("Service available at:");
foreach (var address in accessibleAddresses)
{
    startupLogger.LogInformation("  -> {Address}", address);
}
startupLogger.LogInformation("============================================");

app.Run();