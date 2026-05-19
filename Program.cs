using System.Text;
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
var resultConfig = builder.Configuration.GetSection("ResultConfig");
var authenticationConfig = builder.Configuration.GetSection("Authentication");

var exePath = dataxConfig.GetValue<string>("ExePath") ?? string.Empty;
var code = dataxConfig.GetValue<string>("Code") ?? string.Empty;
var start = dataxConfig.GetValue<string>("Start") ?? string.Empty;
var end = dataxConfig.GetValue<string>("End") ?? string.Empty;
var codesend = dataxConfig.GetValue<string>("Codesend") ?? string.Empty;

var waitSeconds = queueConfig.GetValue<int>("WaitSeconds");
var maxParallelExecutions = queueConfig.GetValue<int>("MaxParallelExecutions");

var outputPath = resultConfig.GetValue<string>("OutputPath") ?? "D:\\";

var authenticationProvider = authenticationConfig.GetValue<string>("Provider") ?? string.Empty;

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

// -----------------------------------------------------------------------
// Repository registrations
// -----------------------------------------------------------------------
builder.Services.AddScoped<RefreshTokenRepository>();
builder.Services.AddScoped<AuthAuditLogRepository>();

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

// Register ExecutionQueue as singleton (shared across the app)
builder.Services.AddSingleton<ExecutionQueue>();

// Register CommandExecutor as singleton with configured parameters
builder.Services.AddSingleton<CommandExecutor>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CommandExecutor>>();
    return new CommandExecutor(exePath, code, start, end, codesend, outputPath, logger);
});

// Register QueuedExecutionService as hosted service (background service)
builder.Services.AddHostedService(sp =>
{
    var queue = sp.GetRequiredService<ExecutionQueue>();
    var executor = sp.GetRequiredService<CommandExecutor>();
    var logger = sp.GetRequiredService<ILogger<QueuedExecutionService>>();
    return new QueuedExecutionService(queue, executor, waitSeconds, maxParallelExecutions, logger);
});

// Register RefreshTokenCleanupService as hosted service (background cleanup)
builder.Services.AddHostedService<RefreshTokenCleanupService>();

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
// Ensure SQLite database is created and migrations are applied on startup
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