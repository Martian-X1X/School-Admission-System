using FluentAssertions;
using SchoolAdmission.Application.DTOs;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Validators;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.UnitTests;

public class ValidatorsTests
{
    [Fact]
    public void CreateClassValidator_ValidInput_CreatesEntity()
    {
        var entity = CreateClassValidator.ValidateAndCreate("  Class 6 - Morning Shift  ", 120);

        entity.Name.Should().Be("Class 6 - Morning Shift");
        entity.TotalSeats.Should().Be(120);
    }

    [Theory]
    [InlineData(null, null, "Class name is required.")]
    [InlineData("", null, "Class name is required.")]
    [InlineData("  ", 10, "Class name is required.")]
    [InlineData("Math", null, "Total seats is required.")]
    [InlineData("Math", 0, "Total seats must be a positive number.")]
    [InlineData("Math", -5, "Total seats must be a positive number.")]
    public void CreateClassValidator_InvalidInput_ThrowsWithMessage(
        string? name, int? totalSeats, string expectedFragment)
    {
        var act = () => CreateClassValidator.ValidateAndCreate(name, totalSeats);

        act.Should().Throw<AppValidationException>().WithMessage($"*{expectedFragment}*");
    }

    [Fact]
    public void CreateClassValidator_NameTooLong_IsRejected()
    {
        var act = () => CreateClassValidator.ValidateAndCreate(new string('x', 201), 10);

        act.Should().Throw<AppValidationException>().WithMessage("*200 characters or fewer*");
    }

    [Fact]
    public void StudentIdValidator_ReturnsValidatedId()
    {
        StudentIdValidator.EnsureValid("20152691234567890").Should().Be("20152691234567890");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("not-a-number")]
    public void StudentIdValidator_Invalid_Throws(string? studentId)
    {
        var act = () => StudentIdValidator.EnsureValid(studentId);

        act.Should().Throw<AppValidationException>().WithMessage("*17-digit*");
    }

    [Theory]
    [InlineData(HoldStatus.Held, "HELD")]
    [InlineData(HoldStatus.Confirmed, "CONFIRMED")]
    [InlineData(HoldStatus.Expired, "EXPIRED")]
    [InlineData(HoldStatus.Cancelled, "CANCELLED")]
    public void HoldStatusText_MapsEnumToApiString(HoldStatus status, string expected)
    {
        status.ToApiString().Should().Be(expected);
    }

    [Fact]
    public void HoldStatus_UsesStableIntegerValues()
    {
        // The partial unique index filter in the database depends on these literals.
        ((int)HoldStatus.Held).Should().Be(1);
        ((int)HoldStatus.Confirmed).Should().Be(2);
        ((int)HoldStatus.Expired).Should().Be(3);
        ((int)HoldStatus.Cancelled).Should().Be(4);
    }
}
