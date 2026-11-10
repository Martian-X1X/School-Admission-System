namespace SchoolAdmission.Domain.Exceptions;

/// <summary>
/// Raised when confirming a hold whose expiration deadline has already passed.
/// The hold is flipped to Expired before this exception leaves the domain method.
/// </summary>
public sealed class HoldExpiredException : DomainException
{
    public Guid HoldId { get; }

    public DateTimeOffset ExpiresAt { get; }

    public HoldExpiredException(Guid holdId, DateTimeOffset expiresAt)
        : base($"Hold {holdId} expired at {expiresAt:O} and can no longer be confirmed.")
    {
        HoldId = holdId;
        ExpiresAt = expiresAt;
    }
}
