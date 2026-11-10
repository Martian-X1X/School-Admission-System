namespace SchoolAdmission.Application.Exceptions;

/// <summary>
/// Raised when an API request fails input validation (HTTP 400).
/// </summary>
public sealed class AppValidationException : Exception
{
    public AppValidationException(string message) : base(message) { }
}
