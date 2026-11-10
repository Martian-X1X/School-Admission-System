namespace SchoolAdmission.Application.Exceptions;

/// <summary>
/// Raised when a requested resource does not exist (HTTP 404).
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.") { }
}
