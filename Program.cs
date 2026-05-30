using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Interfaces;
using ServicioRESTEjecucionComandos.Repositories;
using ServicioRESTEjecucionComandos.Services;

var builder = WebApplication.CreateBuilder(args);

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
        case "postgres":
        case "postgresql":
            options.UseNpgsql(authConnectionString);
            break;
        case "sqlserver":
            options.UseSqlServer(authConnectionString);
            break;
        case "sqlite":
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
        case "postgres":
        case "postgresql":
            options.UseNpgsql(serviceConnectionString);
            break;
        case "sqlserver":
            options.UseSqlServer(serviceConnectionString);
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
    };

    // Allow extraction of token from Authorization header
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Valid authentication token required.\"}");
        },
        OnForbidden = _ => Task.CompletedTask
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// -----------------------------------------------------------------------
// Ensure databases are created on startup
// -----------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

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
                ""CodEnvio"" TEXT NOT NULL,
                ""TipoEntidad"" TEXT NOT NULL,
                ""Codigo"" TEXT NOT NULL,
                ""CronExpression"" TEXT NOT NULL,
                ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                ""CreatedAt"" TEXT NOT NULL,
                ""UpdatedAt"" TEXT NOT NULL
            );
        ");
        scheduleDbContext.Database.ExecuteSqlRaw(@"
            CREATE INDEX IF NOT EXISTS ""IX_etl_schedule_IsActive"" ON ""etl_schedule"" (""IsActive"");
        ");
        scheduleDbContext.Database.ExecuteSqlRaw(@"
            CREATE INDEX IF NOT EXISTS ""IX_etl_schedule_Codigo"" ON ""etl_schedule"" (""Codigo"");
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

        if (serviceDbProvider.ToLower() == "sqlserver")
        {
            // SQL Server syntax
            createTableSql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'hist_etl_execution')
                BEGIN
                    CREATE TABLE [hist_etl_execution] (
                        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        [CodEnvio] NVARCHAR(50) NOT NULL,
                        [TipoEntidad] NVARCHAR(50) NOT NULL,
                        [FechaDatos] DATE NOT NULL,
                        [Codigo] NVARCHAR(50) NOT NULL,
                        [Status] NVARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
                        [TriggerType] NVARCHAR(20) NOT NULL DEFAULT 'MANUAL',
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
        else
        {
            // PostgreSQL syntax (default)
            serviceDbContext.Database.ExecuteSqlRaw(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            
            createTableSql = @"
                CREATE TABLE IF NOT EXISTS ""hist_etl_execution"" (
                    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    ""CodEnvio"" VARCHAR(50) NOT NULL,
                    ""TipoEntidad"" VARCHAR(50) NOT NULL,
                    ""FechaDatos"" DATE NOT NULL,
                    ""Codigo"" VARCHAR(50) NOT NULL,
                    ""Status"" VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
                    ""TriggerType"" VARCHAR(20) NOT NULL DEFAULT 'MANUAL',
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
        logger.LogInformation("Service database schema ensured via raw SQL (hist_etl_execution table created if not exists).");
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

// Redirect root path to index.html
app.MapGet("/", () => Results.Redirect("/index.html"));

// Map API controllers
app.MapControllers();

app.Run();