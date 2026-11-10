using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.Domain.Exceptions;

/// <summary>
/// Raised when a hold state change violates the state machine
/// (e.g. EXPIRED → CONFIRMED or CONFIRMED → CANCELLED).
/// </summary>
public sealed class InvalidHoldTransitionException : DomainException
{
    public HoldStatus From { get; }

    public HoldStatus To { get; }

    public InvalidHoldTransitionException(HoldStatus from, HoldStatus to)
        : base($"A hold in status {from} cannot transition to {to}.")
    {
        From = from;
        To = to;
    }
}
