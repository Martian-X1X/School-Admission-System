using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Middleware;
using SchoolAdmission.Application;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Map the assignment's HOLD_DURATION_SECONDS variable onto Admission:HoldDurationSeconds.
// HOLD_DURATION_SECONDS takes precedence when both are present.
var holdDurationSeconds = Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS");
if (!string.IsNullOrWhiteSpace(holdDurationSeconds))
{
    if (!int.TryParse(holdDurationSeconds, out var parsedSeconds))
    {
        throw new InvalidOperationException(
            "HOLD_DURATION_SECONDS must be an integer number of seconds.");
    }

    builder.Configuration["Admission:HoldDurationSeconds"] = parsedSeconds.ToString();
}

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? Array.Empty<string>();

    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();

// Apply EF Core migrations on startup with retry so `docker compose up`
// fully provisions the database once PostgreSQL accepts connections.
await ApplyMigrationsAsync(app);

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    const int maxAttempts = 30;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await database.Database.MigrateAsync();
            return;
        }
        catch (NpgsqlException) when (attempt < maxAttempts)
        {
            app.Logger.LogInformation(
                "Database not ready yet (attempt {Attempt}/{Max}); retrying in 5s...",
                attempt,
                maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

/// <summary>
/// Exposed for the integration test host (WebApplicationFactory).
/// </summary>
public partial class Program { }
