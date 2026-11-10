namespace SchoolAdmission.Domain.Exceptions;

/// <summary>
/// Raised when a student already holds an active or confirmed application
/// for the same class.
/// </summary>
public sealed class DuplicateApplicationException : DomainException
{
    public Guid ClassId { get; }

    public string StudentId { get; }

    public DuplicateApplicationException(Guid classId, string studentId)
        : base($"Student {studentId} already has an active or confirmed application for class {classId}.")
    {
        ClassId = classId;
        StudentId = studentId;
    }
}
