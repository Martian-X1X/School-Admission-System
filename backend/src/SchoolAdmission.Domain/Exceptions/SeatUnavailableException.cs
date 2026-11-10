namespace SchoolAdmission.Domain.Exceptions;

/// <summary>
/// Raised when a seat reservation is attempted for a class with no remaining capacity.
/// </summary>
public sealed class SeatUnavailableException : DomainException
{
    public Guid ClassId { get; }

    public SeatUnavailableException(Guid classId)
        : base($"No seats are currently available for class {classId}.")
    {
        ClassId = classId;
    }
}
