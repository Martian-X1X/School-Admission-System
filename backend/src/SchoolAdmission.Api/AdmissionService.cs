using Dapper;
using Npgsql;

namespace SchoolAdmission.Api;

public sealed class AdmissionService(NpgsqlDataSource db, AdmissionOptions options)
{
    // The effective status is computed from the DB clock, so a HELD row past expires_at is
    // reported as EXPIRED immediately, without waiting for the background worker.
    private const string EffectiveStatus =
        "CASE WHEN h.status = 'HELD' AND h.expires_at <= clock_timestamp() THEN 'EXPIRED' ELSE h.status END";

    private const string ClassSummarySql = """
        SELECT c.id AS "Id", c.name AS "Name", c.total_seats AS "TotalSeats",
               (COUNT(h.id) FILTER (WHERE h.status = 'HELD' AND h.expires_at > clock_timestamp()))::int AS "HeldSeats",
               (COUNT(h.id) FILTER (WHERE h.status = 'CONFIRMED'))::int AS "ConfirmedSeats"
        FROM admission_classes c
        LEFT JOIN admission_holds h ON h.class_id = c.id
        {WHERE}
        GROUP BY c.id, c.name, c.total_seats, c.created_at
        ORDER BY c.created_at, c.id
        """;

    // ---------- Classes ----------

    public async Task<ClassCreated> CreateClassAsync(string name, int totalSeats, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var id = Guid.NewGuid();
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO admission_classes (id, name, total_seats) VALUES (@id, @name, @totalSeats)",
            new { id, name, totalSeats }, cancellationToken: ct));
        return new ClassCreated { Id = id, Name = name, TotalSeats = totalSeats };
    }

    public async Task<IReadOnlyList<ClassSummary>> ListClassesAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ClassSummary>(new CommandDefinition(
            ClassSummarySql.Replace("{WHERE}", ""), cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<ClassSummary> GetClassAsync(Guid classId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<ClassSummary>(new CommandDefinition(
            ClassSummarySql.Replace("{WHERE}", "WHERE c.id = @classId"), new { classId }, cancellationToken: ct));
        return row ?? throw ApiException.NotFound("Class not found.");
    }

    // ---------- Holds ----------

    /// <summary>
    /// The one transaction that guarantees held + confirmed never exceeds total seats.
    /// All requests for the same class queue on the class row lock (SELECT ... FOR UPDATE), so the
    /// "count occupied seats, then insert" step is serialised per class - across any number of API processes.
    /// </summary>
    public async Task<HoldCreated> CreateHoldAsync(Guid classId, string studentId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 1. Lock the class row.
        var totalSeats = await conn.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT total_seats FROM admission_classes WHERE id = @classId FOR UPDATE",
            new { classId }, tx, cancellationToken: ct));
        if (totalSeats is null) throw ApiException.NotFound("Class not found.");

        // 2. Flip overdue holds of this class to EXPIRED (also frees this student's own unique-index slot).
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE admission_holds SET status = 'EXPIRED'
            WHERE class_id = @classId AND status = 'HELD' AND expires_at <= clock_timestamp()
            """, new { classId }, tx, cancellationToken: ct));

        // 3. One active application per student per class.
        var alreadyApplied = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM admission_holds
                WHERE class_id = @classId
                AND student_id = @studentId
                AND (
                    status = 'CONFIRMED'
                    OR (status = 'HELD' AND expires_at > clock_timestamp())
                )
            )
            """, new { classId, studentId }, tx, cancellationToken: ct));
        if (alreadyApplied)
            throw ApiException.Conflict("ALREADY_APPLIED", "This student already has an active application for this class.");

       
        // 4. Capacity check.
        var occupied = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)::int
                FROM admission_holds
                WHERE class_id = @classId
                AND (
                    status = 'CONFIRMED'
                    OR (status = 'HELD' AND expires_at > clock_timestamp())
                )
                """,
                new { classId }, tx, cancellationToken: ct));

        if (occupied >= totalSeats)
        {
            throw ApiException.Conflict(
                "SEATS_FULL",
                "No seats are available in this class.");
        }

        // 5. Insert the hold.
        try
        {
            var hold = await conn.QuerySingleAsync<HoldCreated>(
                new CommandDefinition(
                    """
                    WITH t AS (SELECT clock_timestamp() AS n)
                    INSERT INTO admission_holds
                        (id, class_id, student_id, status, created_at, expires_at)
                    SELECT
                        @id, @classId, @studentId, 'HELD',
                        t.n,
                        t.n + make_interval(secs => @secs::float8)
                    FROM t
                    RETURNING
                        id AS "HoldId",
                        class_id AS "ClassId",
                        student_id AS "StudentId",
                        status AS "Status",
                        expires_at AS "ExpiresAt"
                    """,
                    new
                    {
                        id = Guid.NewGuid(),
                        classId,
                        studentId,
                        secs = options.HoldDurationSeconds
                    },
                    tx,
                    cancellationToken: ct));

            await tx.CommitAsync(ct);
            return hold;
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                && ex.ConstraintName == "uq_hold_active_student_class")
        {
            throw ApiException.Conflict(
                "ALREADY_APPLIED",
                "This student already has an active application for this class.");
        }
    }


    public async Task<HoldView> GetHoldAsync(Guid holdId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<HoldView>(new CommandDefinition(
            $"""
            SELECT h.id AS "HoldId", h.class_id AS "ClassId", {EffectiveStatus} AS "Status", h.expires_at AS "ExpiresAt"
            FROM admission_holds h WHERE h.id = @holdId
            """, new { holdId }, cancellationToken: ct));
        return row ?? throw ApiException.NotFound("Application not found.");
    }

    public async Task<HoldConfirmed> ConfirmAsync(Guid holdId, string studentId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var hold = await LockHoldAsync(conn, tx, holdId, ct);
        if (hold.StudentId != studentId)
            throw ApiException.Forbidden("This application belongs to another student.");

        switch (hold.Status)
        {
            case "CONFIRMED": return new HoldConfirmed { HoldId = holdId, Status = "CONFIRMED" }; // idempotent
            case "CANCELLED": throw ApiException.Conflict("HOLD_CANCELLED", "This application was cancelled.");
            case "EXPIRED": throw ApiException.Conflict("HOLD_EXPIRED", "This application has expired.");
        }

        // Status is HELD: confirm only if it has not expired - judged by the DB clock *after* the lock was acquired.
        var updated = await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE admission_holds SET status = 'CONFIRMED', confirmed_at = clock_timestamp()
            WHERE id = @holdId AND status = 'HELD' AND expires_at > clock_timestamp()
            """, new { holdId }, tx, cancellationToken: ct));
        if (updated == 1)
        {
            await tx.CommitAsync(ct);
            return new HoldConfirmed { HoldId = holdId, Status = "CONFIRMED" };
        }

        await MarkExpiredAsync(conn, tx, holdId, ct);
        await tx.CommitAsync(ct); // persist EXPIRED, then report the conflict
        throw ApiException.Conflict("HOLD_EXPIRED", "This application has expired.");
    }

    public async Task CancelAsync(Guid holdId, string studentId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var hold = await LockHoldAsync(conn, tx, holdId, ct);
        if (hold.StudentId != studentId)
            throw ApiException.Forbidden("This application belongs to another student.");

        switch (hold.Status)
        {
            case "CANCELLED": return; // idempotent
            case "CONFIRMED": throw ApiException.Conflict("ALREADY_CONFIRMED", "A confirmed admission cannot be cancelled.");
            case "EXPIRED": throw ApiException.Conflict("HOLD_EXPIRED", "This application has already expired.");
        }

        var updated = await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE admission_holds SET status = 'CANCELLED'
            WHERE id = @holdId AND status = 'HELD' AND expires_at > clock_timestamp()
            """, new { holdId }, tx, cancellationToken: ct));
        if (updated == 1)
        {
            await tx.CommitAsync(ct);
            return;
        }

        await MarkExpiredAsync(conn, tx, holdId, ct);
        await tx.CommitAsync(ct);
        throw ApiException.Conflict("HOLD_EXPIRED", "This application has already expired.");
    }

    public async Task<IReadOnlyList<MyHold>> MyHoldsAsync(string studentId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<MyHold>(new CommandDefinition(
            $"""
            SELECT h.id AS "HoldId", h.class_id AS "ClassId", c.name AS "ClassName",
                   {EffectiveStatus} AS "Status", h.expires_at AS "ExpiresAt", h.created_at AS "CreatedAt"
            FROM admission_holds h
            JOIN admission_classes c ON c.id = h.class_id
            WHERE h.student_id = @studentId
            ORDER BY h.created_at DESC
            """, new { studentId }, cancellationToken: ct));
        return rows.AsList();
    }

    // ---------- helpers ----------

    private static async Task<LockedHold> LockHoldAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid holdId, CancellationToken ct)
    {
        var hold = await conn.QuerySingleOrDefaultAsync<LockedHold>(new CommandDefinition(
            """
            SELECT id AS "Id", student_id AS "StudentId", status AS "Status"
            FROM admission_holds WHERE id = @holdId FOR UPDATE
            """, new { holdId }, tx, cancellationToken: ct));
        return hold ?? throw ApiException.NotFound("Application not found.");
    }

    private static Task<int> MarkExpiredAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid holdId, CancellationToken ct) =>
        conn.ExecuteAsync(new CommandDefinition(
            "UPDATE admission_holds SET status = 'EXPIRED' WHERE id = @holdId AND status = 'HELD'",
            new { holdId }, tx, cancellationToken: ct));
}
