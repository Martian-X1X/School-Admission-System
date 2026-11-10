using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Domain.Common;

namespace SchoolAdmission.Application.Validators;

public static class StudentIdValidator
{
    public static string EnsureValid(string? studentId)
    {
        if (!StudentIdentifier.IsValid(studentId))
        {
            throw new AppValidationException(
                "A valid 17-digit student birth registration number is required (X-Student-Id).");
        }

        return studentId!;
    }
}
