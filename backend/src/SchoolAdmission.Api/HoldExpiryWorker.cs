using Dapper;
using Npgsql;

namespace SchoolAdmission.Api;

/// <summary>
/// Tidies up the stored status of overdue holds. It is NOT the authority on seat availability:
/// every query already ignores HELD rows whose expires_at has passed.
/// </summary>
public sealed class HoldExpiryWorker(NpgsqlDataSource db, AdmissionOptions options, ILogger<HoldExpiryWorker> log)
    : BackgroundService
{
    private const int BatchSize = 500;

    private const string SweepSql = """
        WITH overdue AS (
            SELECT id FROM admission_holds
            WHERE status = 'HELD' AND expires_at <= clock_timestamp()
            ORDER BY expires_at
            LIMIT 500
            FOR UPDATE SKIP LOCKED
        )
        UPDATE admission_holds h
        SET status = 'EXPIRED'
        FROM overdue o
        WHERE h.id = o.id
        """;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(options.ExpirySweepSeconds);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                int n;
                do
                {
                    await using var conn = await db.OpenConnectionAsync(ct);
                    n = await conn.ExecuteAsync(new CommandDefinition(SweepSql, cancellationToken: ct));
                    if (n > 0) log.LogInformation("Expired {Count} overdue hold(s).", n);
                } while (n >= BatchSize && !ct.IsCancellationRequested);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Expiry sweep failed; will retry.");
            }

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
