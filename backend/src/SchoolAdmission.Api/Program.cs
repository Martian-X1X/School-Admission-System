using Npgsql;
using SchoolAdmission.Api;

var builder = WebApplication.CreateBuilder(args);

// ---- configuration (read lazily so tests can override services) ----
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    int Read(string key, int fallback) => int.TryParse(cfg[key], out var v) && v >= 1 ? v : fallback;
    return new AdmissionOptions(Read("HOLD_DURATION_SECONDS", 600), Read("EXPIRY_SWEEP_SECONDS", 2));
});

builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var cs = cfg.GetConnectionString("Default")
             ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
    var csb = new NpgsqlConnectionStringBuilder(cs)
    {
        MaxPoolSize = int.TryParse(cfg["DB_MAX_POOL_SIZE"], out var p) && p > 0 ? p : 40
    };
    return NpgsqlDataSource.Create(csb.ConnectionString);
});

builder.Services.AddSingleton<AdmissionService>();
builder.Services.AddHostedService<SchemaInitializer>();   // creates tables before the sweeper starts
builder.Services.AddHostedService<HoldExpiryWorker>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Date")));

var app = builder.Build();

app.UseCors();

// ---- one place that turns exceptions into JSON error bodies; client mistakes are always 4xx ----
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (ApiException ex)
    {
        await WriteError(ctx, ex.Status, ex.Code, ex.Message);
    }
    catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
    {
        // client went away
    }
    catch (NpgsqlException ex) when (ex is not PostgresException)
    {
        app.Logger.LogError(ex, "Database unavailable");
        await WriteError(ctx, 503, "DB_UNAVAILABLE", "The database is temporarily unavailable.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled error");
        await WriteError(ctx, 500, "INTERNAL", "Something went wrong on the server.");
    }
});

static async Task WriteError(HttpContext ctx, int status, string code, string detail)
{
    if (ctx.Response.HasStarted) return;
    ctx.Response.Clear();
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { type = "about:blank", title = code, status, code, detail });
}

// ---- health ----
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (NpgsqlDataSource db, CancellationToken ct) =>
{
    try
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT to_regclass('public.admission_holds') IS NOT NULL", conn);
        var ready = (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
        return ready ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

// ---- classes ----
app.MapPost("/classes", async (HttpRequest req, AdmissionService svc, CancellationToken ct) =>
{
    var body = await Helpers.ReadBodyAsync<CreateClassRequest>(req, ct);
    var name = body.Name?.Trim();
    if (string.IsNullOrEmpty(name)) throw ApiException.Validation("name is required.");
    if (name.Length > 200) throw ApiException.Validation("name must be at most 200 characters.");
    if (body.TotalSeats is null or < 1 or > 100000)
        throw ApiException.Validation("totalSeats must be a whole number between 1 and 100000.");

    var created = await svc.CreateClassAsync(name, body.TotalSeats.Value, ct);
    return Results.Created($"/classes/{created.Id}", created);
});

app.MapGet("/classes", async (AdmissionService svc, CancellationToken ct) =>
    Results.Ok(await svc.ListClassesAsync(ct)));

app.MapGet("/classes/{classId}", async (string classId, AdmissionService svc, CancellationToken ct) =>
    Results.Ok(await svc.GetClassAsync(Helpers.ParseId(classId, "Class"), ct)));

// ---- holds ----
app.MapPost("/classes/{classId}/holds", async (string classId, HttpRequest req, AdmissionService svc, CancellationToken ct) =>
{
    var studentId = Helpers.RequireStudentId(req);
    var hold = await svc.CreateHoldAsync(Helpers.ParseId(classId, "Class"), studentId, ct);
    return Results.Created($"/holds/{hold.HoldId}", hold);
});

app.MapGet("/holds/{holdId}", async (string holdId, AdmissionService svc, CancellationToken ct) =>
    Results.Ok(await svc.GetHoldAsync(Helpers.ParseId(holdId, "Application"), ct)));

app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpRequest req, AdmissionService svc, CancellationToken ct) =>
{
    var studentId = Helpers.RequireStudentId(req);
    return Results.Ok(await svc.ConfirmAsync(Helpers.ParseId(holdId, "Application"), studentId, ct));
});

app.MapDelete("/holds/{holdId}", async (string holdId, HttpRequest req, AdmissionService svc, CancellationToken ct) =>
{
    var studentId = Helpers.RequireStudentId(req);
    await svc.CancelAsync(Helpers.ParseId(holdId, "Application"), studentId, ct);
    return Results.NoContent();
});

app.MapGet("/me/holds", async (HttpRequest req, AdmissionService svc, CancellationToken ct) =>
    Results.Ok(await svc.MyHoldsAsync(Helpers.RequireStudentId(req), ct)));

app.Run();

public partial class Program { }
