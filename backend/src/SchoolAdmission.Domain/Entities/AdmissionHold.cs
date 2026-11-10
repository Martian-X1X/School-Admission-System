using SchoolAdmission.Domain.Common;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Domain.Exceptions;

namespace SchoolAdmission.Domain.Entities;

public class AdmissionHold
{
    public Guid Id { get; private set; }

    public Guid ClassId { get; private set; }

    public string StudentId { get; private set; } = "";

    public HoldStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    private AdmissionHold() { }

    public AdmissionHold(
        Guid classId,
        string studentId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (!StudentIdentifier.IsValid(studentId))
            throw new ArgumentException(
                "Student ID must be a 17-digit birth registration number.",
                nameof(studentId));

        if (expiresAt <= createdAt)
            throw new ArgumentException(
                "Expiration must be after creation.",
                nameof(expiresAt));

        Id = Guid.NewGuid();
        ClassId = classId;
        StudentId = studentId;
        Status = HoldStatus.Held;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Rebuilds an existing hold (as read from persistence) without generating new identity.
    /// </summary>
    public static AdmissionHold Restore(
        Guid id,
        Guid classId,
        string studentId,
        HoldStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? confirmedAt) =>
        new()
        {
            Id = id,
            ClassId = classId,
            StudentId = studentId,
            Status = status,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            ConfirmedAt = confirmedAt
        };

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Marks the hold as expired. Idempotent: returns false when not currently held.
    /// </summary>
    public bool MarkExpired()
    {
        if (Status != HoldStatus.Held)
        {
            return false;
        }

        Status = HoldStatus.Expired;
        return true;
    }

    /// <summary>
    /// Confirms the reservation. Idempotent: confirming an already-confirmed hold is a no-op.
    /// Throws <see cref="HoldExpiredException"/> (after flipping status to Expired) when the
    /// hold's deadline has passed, and <see cref="InvalidHoldTransitionException"/> for
    /// cancelled or already expired holds.
    /// </summary>
    public void Confirm(DateTimeOffset now)
    {
        if (Status == HoldStatus.Confirmed)
        {
            return;
        }

        if (Status != HoldStatus.Held)
        {
            throw new InvalidHoldTransitionException(Status, HoldStatus.Confirmed);
        }

        if (IsExpired(now))
        {
            Status = HoldStatus.Expired;
            throw new HoldExpiredException(Id, ExpiresAt);
        }

        Status = HoldStatus.Confirmed;
        ConfirmedAt = now;
    }

    /// <summary>
    /// Cancels the reservation. Idempotent: cancelling an already-cancelled hold is a no-op.
    /// Confirmed or expired holds cannot be cancelled.
    /// </summary>
    public void Cancel()
    {
        if (Status == HoldStatus.Cancelled)
        {
            return;
        }

        if (Status != HoldStatus.Held)
        {
            throw new InvalidHoldTransitionException(Status, HoldStatus.Cancelled);
        }

        Status = HoldStatus.Cancelled;
    }
}
