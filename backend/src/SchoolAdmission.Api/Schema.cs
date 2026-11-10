using Dapper;
using Npgsql;

namespace SchoolAdmission.Api;

public static class Schema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS admission_classes (
            id           UUID PRIMARY KEY,
            name         VARCHAR(200) NOT NULL,
            total_seats  INTEGER NOT NULL,
            created_at   TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
            CONSTRAINT ck_class_name CHECK (length(btrim(name)) > 0),
            CONSTRAINT ck_class_total_seats CHECK (total_seats > 0)
        );

        CREATE TABLE IF NOT EXISTS admission_holds (
            id            UUID PRIMARY KEY,
            class_id      UUID NOT NULL REFERENCES admission_classes(id) ON DELETE RESTRICT,
            student_id    VARCHAR(17) NOT NULL,
            status        VARCHAR(12) NOT NULL,
            created_at    TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
            expires_at    TIMESTAMPTZ NOT NULL,
            confirmed_at  TIMESTAMPTZ NULL,
            CONSTRAINT ck_hold_student_id CHECK (student_id ~ '^[0-9]{17}$'),
            CONSTRAINT ck_hold_status CHECK (status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')),
            CONSTRAINT ck_hold_expiry CHECK (expires_at > created_at),
            CONSTRAINT ck_hold_confirmed_at CHECK (
                (status = 'CONFIRMED' AND confirmed_at IS NOT NULL) OR
                (status <> 'CONFIRMED' AND confirmed_at IS NULL))
        );

        -- One active (HELD or CONFIRMED) application per student per class.
        CREATE UNIQUE INDEX IF NOT EXISTS uq_hold_active_student_class
            ON admission_holds (class_id, student_id)
            WHERE status IN ('HELD','CONFIRMED');

        CREATE INDEX IF NOT EXISTS ix_holds_student ON admission_holds (student_id, created_at DESC);
        CREATE INDEX IF NOT EXISTS ix_holds_class_status ON admission_holds (class_id, status);
        CREATE INDEX IF NOT EXISTS ix_holds_expiry ON admission_holds (expires_at) WHERE status = 'HELD';
        """;

    public static async Task EnsureAsync(NpgsqlDataSource db, ILogger log, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var conn = await db.OpenConnectionAsync(ct);
                await using var tx = await conn.BeginTransactionAsync(ct);
                // Serialises schema creation if two API processes start at the same time.
                await conn.ExecuteAsync(new CommandDefinition(
                    "SELECT pg_advisory_xact_lock(727274)", transaction: tx, cancellationToken: ct));
                await conn.ExecuteAsync(new CommandDefinition(Sql, transaction: tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
                log.LogInformation("Database schema is ready.");
                return;
            }
            catch (Exception ex) when (attempt < 30 && ex is not OperationCanceledException)
            {
                log.LogWarning("Database not ready (attempt {Attempt}/30): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }
}

public sealed class SchemaInitializer(NpgsqlDataSource db, ILogger<SchemaInitializer> log) : IHostedService
{
    public Task StartAsync(CancellationToken ct) => Schema.EnsureAsync(db, log, ct);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
