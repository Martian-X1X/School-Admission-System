namespace SchoolAdmission.Domain.Exceptions;

/// <summary>
/// Base type for business-rule violations raised by the domain layer.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
