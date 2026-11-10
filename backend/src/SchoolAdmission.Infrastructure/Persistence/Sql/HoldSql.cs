namespace SchoolAdmission.Infrastructure.Persistence.Sql;

/// <summary>
/// Transactional SQL used by the hold repository.
///
/// Status literals: 1 = HELD, 2 = CONFIRMED, 3 = EXPIRED, 4 = CANCELLED.
/// All time comparisons use PostgreSQL clock_timestamp() so every application
/// instance agrees on the authoritative time.
/// </summary>
internal static class HoldSql
{
    /// <summary>Locks the class row, serializing competing seat reservations (FCFS).</summary>
    internal const string LockClass = """
        SELECT id, name, total_seats, created_at
        FROM admission_classes
        WHERE id = @class_id
        FOR UPDATE;
        """;

    /// <summary>Lazily flips overdue HELD rows of this class to EXPIRED inside the reservation transaction.</summary>
    internal const string ExpireOverdueInClass = """
        UPDATE admission_holds
        SET status = 3
        WHERE class_id = @class_id
          AND status = 1
          AND expires_at <= clock_timestamp();
        """;

    /// <summary>Duplicate-application check (independent of the lazy expiry above).</summary>
    internal const string HasActiveApplication = """
        SELECT EXISTS (
            SELECT 1
            FROM admission_holds
            WHERE class_id = @class_id
              AND student_id = @student_id
              AND (status = 2 OR (status = 1 AND expires_at > clock_timestamp())));
        """;

    /// <summary>Occupancy recount: non-expired HELD plus CONFIRMED rows.</summary>
    internal const string CountOccupiedSeats = """
        SELECT count(*)
        FROM admission_holds
        WHERE class_id = @class_id
          AND (status = 2 OR (status = 1 AND expires_at > clock_timestamp()));
        """;

    /// <summary>Creates the hold; expiration is computed from the database clock.</summary>
    internal const string InsertHold = """
        INSERT INTO admission_holds (id, class_id, student_id, status, created_at, expires_at, confirmed_at)
        VALUES (@id, @class_id, @student_id, 1, clock_timestamp(), clock_timestamp() + make_interval(secs => @hold_seconds), NULL)
        RETURNING created_at, expires_at;
        """;

    /// <summary>Reads and locks one hold row together with the authoritative database time.</summary>
    internal const string LockHold = """
        SELECT id, class_id, student_id, status, created_at, expires_at, confirmed_at,
               clock_timestamp() AS db_now
        FROM admission_holds
        WHERE id = @hold_id
        FOR UPDATE;
        """;

    /// <summary>Reads one hold row together with the authoritative database time (no lock).</summary>
    internal const string ReadHold = """
        SELECT id, class_id, student_id, status, created_at, expires_at, confirmed_at,
               clock_timestamp() AS db_now
        FROM admission_holds
        WHERE id = @hold_id;
        """;

    /// <summary>Reads a student's applications (joined with the class name) with effective statuses.</summary>
    internal const string GetByStudentId = """
        SELECT h.id, h.class_id, h.student_id, h.status, h.created_at, h.expires_at, h.confirmed_at,
               c.name AS class_name,
               clock_timestamp() AS db_now
        FROM admission_holds h
        JOIN admission_classes c ON c.id = h.class_id
        WHERE h.student_id = @student_id
        ORDER BY h.created_at DESC;
        """;

    internal const string MarkExpired = """
        UPDATE admission_holds
        SET status = 3
        WHERE id = @hold_id
          AND status = 1;
        """;

    internal const string ConfirmHold = """
        UPDATE admission_holds
        SET status = 2, confirmed_at = @confirmed_at
        WHERE id = @hold_id
          AND status = 1;
        """;

    internal const string CancelHold = """
        UPDATE admission_holds
        SET status = 4
        WHERE id = @hold_id
          AND status = 1;
        """;

    /// <summary>Batched expiry used by the background worker.</summary>
    internal const string ExpireOverdueBatch = """
        UPDATE admission_holds
        SET status = 3
        WHERE id IN (
            SELECT id
            FROM admission_holds
            WHERE status = 1
              AND expires_at <= clock_timestamp()
            LIMIT @batch_size);
        """;
}
