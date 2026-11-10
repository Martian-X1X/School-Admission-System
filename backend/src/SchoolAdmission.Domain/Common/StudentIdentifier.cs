using System.Text.RegularExpressions;

namespace SchoolAdmission.Domain.Common;

/// <summary>
/// Validates 17-digit student birth registration numbers.
/// </summary>
public static class StudentIdentifier
{
    private static readonly Regex Pattern = new("^[0-9]{17}$", RegexOptions.Compiled);

    public static bool IsValid(string? studentId) =>
        !string.IsNullOrEmpty(studentId) && Pattern.IsMatch(studentId);
}
