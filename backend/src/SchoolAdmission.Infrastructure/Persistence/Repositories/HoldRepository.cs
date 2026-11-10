using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Interfaces;
using SchoolAdmission.Application.Models;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Domain.Exceptions;
using SchoolAdmission.Infrastructure.Persistence.Sql;

namespace SchoolAdmission.Infrastructure.Persistence.Repositories;

/// <summary>
/// Transactional implementation of the seat-reservation operations.
///
/// Every mutating operation runs inside a single database transaction:
///   CreateHold — locks the class row (FOR UPDATE), lazily expires overdue holds,
///   re-checks duplicates and occupancy, then inserts; capacity can never be exceeded.
///   Confirm/Cancel — lock the hold row (FOR UPDATE), validate ownership and the state
///   machine against clock_timestamp(), then persist the transition.
/// </summary>
public class HoldRepository : IHoldRepository
{
    private const string UniqueViolationSqlState = "23505";

    private readonly AppDbContext _db;

    public HoldRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdmissionHold> CreateHoldAsync(
        Guid classId,
        string studentId,
        TimeSpan holdDuration,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var connection = await GetOpenConnectionAsync(cancellationToken);

        var totalSeats = await LockClassRowAsync(connection, classId, cancellationToken);

        await ExecuteAsync(connection, HoldSql.ExpireOverdueInClass, cancellationToken,
            ("class_id", classId));

        var hasActiveApplication = await ScalarBoolAsync(
            connection, HoldSql.HasActiveApplication, cancellationToken,
            ("class_id", classId), ("student_id", studentId));

        if (hasActiveApplication)
        {
            throw new DuplicateApplicationException(classId, studentId);
        }

        var occupiedSeats = await ScalarIntAsync(
            connection, HoldSql.CountOccupiedSeats, cancellationToken,
            ("class_id", classId));

        if (occupiedSeats >= totalSeats)
        {
            throw new SeatUnavailableException(classId);
        }

        var holdId = Guid.NewGuid();
        try
        {
            await using var command = new NpgsqlCommand(HoldSql.InsertHold, connection);
            command.Parameters.AddWithValue("id", holdId);
            command.Parameters.AddWithValue("class_id", classId);
            command.Parameters.AddWithValue("student_id", studentId);
            command.Parameters.AddWithValue("hold_seconds", holdDuration.TotalSeconds);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
            var expiresAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at"));

            await transaction.CommitAsync(cancellationToken);

            return AdmissionHold.Restore(
                holdId, classId, studentId, HoldStatus.Held, createdAt, expiresAt, null);
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolationSqlState)
        {
            // Partial unique index backstop: concurrent duplicate application for the same class+student.
            throw new DuplicateApplicationException(classId, studentId);
        }
    }

    public async Task<AdmissionHold?> FindByIdAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var row = await ReadHoldRowAsync(connection, HoldSql.ReadHold, holdId, cancellationToken);

        if (row is null)
        {
            return null;
        }

        // Lazy expiration: persist the stored status when the deadline has passed.
        if (row.Hold.Status == HoldStatus.Held && row.Hold.IsExpired(row.DbNow))
        {
            row.Hold.MarkExpired();
            await ExecuteAsync(connection, HoldSql.MarkExpired, cancellationToken, ("hold_id", holdId));
        }

        return row.Hold;
    }

    public async Task<AdmissionHold> ConfirmAsync(Guid holdId, string studentId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var connection = await GetOpenConnectionAsync(cancellationToken);

        var row = await ReadHoldRowAsync(connection, HoldSql.LockHold, holdId, cancellationToken)
            ?? throw new NotFoundException("Hold", holdId);

        if (row.Hold.StudentId != studentId)
        {
            throw new OwnershipException("hold", holdId);
        }

        var statusBefore = row.Hold.Status;

        try
        {
            row.Hold.Confirm(row.DbNow);
        }
        catch (HoldExpiredException)
        {
            // The domain method flipped the hold to Expired; persist that transition.
            await ExecuteAsync(connection, HoldSql.MarkExpired, cancellationToken, ("hold_id", holdId));
            await transaction.CommitAsync(cancellationToken);
            throw;
        }

        if (statusBefore == HoldStatus.Confirmed)
        {
            // Idempotent confirmation: nothing to change.
            await transaction.CommitAsync(cancellationToken);
            return row.Hold;
        }

        await ExecuteAsync(connection, HoldSql.ConfirmHold, cancellationToken,
            ("hold_id", holdId), ("confirmed_at", row.DbNow));

        await transaction.CommitAsync(cancellationToken);
        return row.Hold;
    }

    public async Task<AdmissionHold> CancelAsync(Guid holdId, string studentId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var connection = await GetOpenConnectionAsync(cancellationToken);

        var row = await ReadHoldRowAsync(connection, HoldSql.LockHold, holdId, cancellationToken)
            ?? throw new NotFoundException("Hold", holdId);

        if (row.Hold.StudentId != studentId)
        {
            throw new OwnershipException("hold", holdId);
        }

        var statusBefore = row.Hold.Status;
        row.Hold.Cancel();

        if (statusBefore == HoldStatus.Cancelled)
        {
            // Idempotent cancellation: nothing to change.
            await transaction.CommitAsync(cancellationToken);
            return row.Hold;
        }

        await ExecuteAsync(connection, HoldSql.CancelHold, cancellationToken, ("hold_id", holdId));
        await transaction.CommitAsync(cancellationToken);
        return row.Hold;
    }

    public async Task<IReadOnlyList<StudentHoldRecord>> GetByStudentIdAsync(
        string studentId,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var results = new List<StudentHoldRecord>();

        await using var command = new NpgsqlCommand(HoldSql.GetByStudentId, connection);
        command.Parameters.AddWithValue("student_id", studentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var hold = ReadHoldEntity(reader);
            var className = reader.GetString(reader.GetOrdinal("class_name"));
            var dbNow = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("db_now"));

            // Effective status: an unflipped but overdue HELD row is already expired.
            var effectiveStatus = hold.Status == HoldStatus.Held && hold.IsExpired(dbNow)
                ? HoldStatus.Expired
                : hold.Status;

            results.Add(new StudentHoldRecord(
                AdmissionHold.Restore(
                    hold.Id, hold.ClassId, hold.StudentId, effectiveStatus,
                    hold.CreatedAt, hold.ExpiresAt, hold.ConfirmedAt),
                className));
        }

        return results;
    }

    public async Task<int> ExpireOverdueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(HoldSql.ExpireOverdueBatch, connection);
        command.Parameters.AddWithValue("batch_size", batchSize);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<HoldRow?> ReadHoldRowAsync(
        NpgsqlConnection connection,
        string sql,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("hold_id", holdId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var dbNowOrdinal = reader.GetOrdinal("db_now");
        return new HoldRow(ReadHoldEntity(reader), reader.GetFieldValue<DateTimeOffset>(dbNowOrdinal));
    }

    private static AdmissionHold ReadHoldEntity(NpgsqlDataReader reader) =>
        AdmissionHold.Restore(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetGuid(reader.GetOrdinal("class_id")),
            reader.GetString(reader.GetOrdinal("student_id")),
            (HoldStatus)reader.GetInt32(reader.GetOrdinal("status")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at")),
            reader.IsDBNull(reader.GetOrdinal("confirmed_at"))
                ? null
                : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("confirmed_at")));

    private async Task<int> LockClassRowAsync(
        NpgsqlConnection connection,
        Guid classId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(HoldSql.LockClass, connection);
        command.Parameters.AddWithValue("class_id", classId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new NotFoundException("Class", classId);
        }

        return reader.GetInt32(reader.GetOrdinal("total_seats"));
    }

    private static async Task<bool> ScalarBoolAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static async Task<int> ScalarIntAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private sealed record HoldRow(AdmissionHold Hold, DateTimeOffset DbNow);
}
