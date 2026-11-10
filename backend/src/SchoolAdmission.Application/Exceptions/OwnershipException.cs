namespace SchoolAdmission.Application.Exceptions;

/// <summary>
/// Raised when a student attempts to operate on a hold they do not own (HTTP 403).
/// </summary>
public sealed class OwnershipException : Exception
{
    public OwnershipException(string resource, object key)
        : base($"Student does not own {resource} '{key}'.") { }
}
