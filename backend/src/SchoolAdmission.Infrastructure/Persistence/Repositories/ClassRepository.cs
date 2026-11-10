using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Application.Interfaces;
using SchoolAdmission.Application.Models;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Infrastructure.Persistence.Repositories;

public class ClassRepository : IClassRepository
{
    private readonly AppDbContext _db;

    public ClassRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdmissionClass> AddAsync(AdmissionClass admissionClass, CancellationToken cancellationToken = default)
    {
        _db.AdmissionClasses.Add(admissionClass);
        await _db.SaveChangesAsync(cancellationToken);
        return admissionClass;
    }

    public async Task<ClassWithSeats?> FindAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(ClassSql.SelectById, connection);
        command.Parameters.AddWithValue("class_id", classId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadClassWithSeats(reader);
    }

    public async Task<IReadOnlyList<ClassWithSeats>> ListAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var results = new List<ClassWithSeats>();

        await using var command = new NpgsqlCommand(ClassSql.SelectAll, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadClassWithSeats(reader));
        }

        return results;
    }

    private static ClassWithSeats ReadClassWithSeats(NpgsqlDataReader reader)
    {
        var admissionClass = AdmissionClass.Restore(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("name")),
            reader.GetInt32(reader.GetOrdinal("total_seats")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")));

        var held = (int)reader.GetInt64(reader.GetOrdinal("held_seats"));
        var confirmed = (int)reader.GetInt64(reader.GetOrdinal("confirmed_seats"));

        return new ClassWithSeats(admissionClass, new SeatSummary(admissionClass.TotalSeats, held, confirmed));
    }

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private static class ClassSql
    {
        internal const string SelectColumns = """
            SELECT c.id, c.name, c.total_seats, c.created_at,
                   (SELECT count(*) FROM admission_holds h
                     WHERE h.class_id = c.id
                       AND h.status = 1
                       AND h.expires_at > clock_timestamp()) AS held_seats,
                   (SELECT count(*) FROM admission_holds h
                     WHERE h.class_id = c.id
                       AND h.status = 2) AS confirmed_seats
            """;

        internal const string SelectAll = $"""
            {SelectColumns}
            FROM admission_classes c
            ORDER BY c.created_at;
            """;

        internal const string SelectById = $"""
            {SelectColumns}
            FROM admission_classes c
            WHERE c.id = @class_id;
            """;
    }
}
