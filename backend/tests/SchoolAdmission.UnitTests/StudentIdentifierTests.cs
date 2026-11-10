using FluentAssertions;
using SchoolAdmission.Domain.Common;

namespace SchoolAdmission.UnitTests;

public class StudentIdentifierTests
{
    [Theory]
    [InlineData("20152691234567890")]
    [InlineData("00000000000000000")]
    [InlineData("99999999999999999")]
    public void IsValid_AcceptsExactly17Digits(string studentId)
    {
        StudentIdentifier.IsValid(studentId).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2015269123456789")]      // 16 digits
    [InlineData("201526912345678901")]     // 18 digits
    [InlineData("2015269123456789a")]      // letter
    [InlineData("201526912345678 9")]      // space
    [InlineData("201526912345678.0")]      // punctuation
    [InlineData("+8801515269123456")]     // formatting
    public void IsValid_RejectsAnythingElse(string? studentId)
    {
        StudentIdentifier.IsValid(studentId).Should().BeFalse();
    }
}
