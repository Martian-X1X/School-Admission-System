using SchoolAdmission.Application.Models;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Interfaces;

/// <summary>
/// Focused repository abstraction around the transactional seat-reservation
/// operations that need explicit row locking and database-clock semantics.
/// </summary>
public interface IHoldRepository
{
    /// <summary>
    /// Reserves one seat. Enforces the class capacity invariant under concurrency.
    /// Throws NotFound, DuplicateApplication or SeatUnavailable exceptions.
    /// </summary>
    Task<AdmissionHold> CreateHoldAsync(Guid classId, string studentId, TimeSpan holdDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a hold; lazily persists EXPIRED when its deadline has passed.
    /// </summary>
    Task<AdmissionHold?> FindByIdAsync(Guid holdId, CancellationToken cancellationToken = default);

    Task<AdmissionHold> ConfirmAsync(Guid holdId, string studentId, CancellationToken cancellationToken = default);

    Task<AdmissionHold> CancelAsync(Guid holdId, string studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentHoldRecord>> GetByStudentIdAsync(string studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-expires overdue HELD rows. Returns the number of rows flipped.
    /// </summary>
    Task<int> ExpireOverdueAsync(int batchSize, CancellationToken cancellationToken = default);
}
