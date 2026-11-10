using FluentAssertions;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Domain.Exceptions;

namespace SchoolAdmission.UnitTests;

public class AdmissionHoldTests
{
    private static readonly string ValidStudentId = "20152691234567890";

    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.UtcNow;

    private static AdmissionHold NewHold(DateTimeOffset? expiresAt = null) =>
        new(Guid.NewGuid(), ValidStudentId, CreatedAt, expiresAt ?? CreatedAt.AddMinutes(10));

    [Fact]
    public void Constructor_AcceptsValidInput()
    {
        var expiresAt = CreatedAt.AddMinutes(10);

        var hold = new AdmissionHold(Guid.NewGuid(), ValidStudentId, CreatedAt, expiresAt);

        hold.Id.Should().NotBeEmpty();
        hold.Status.Should().Be(HoldStatus.Held);
        hold.ConfirmedAt.Should().BeNull();
        hold.ExpiresAt.Should().Be(expiresAt);
    }

    [Theory]
    [InlineData("2015269123456789")]
    [InlineData("201526912345678901")]
    [InlineData("2015269123456789a")]
    [InlineData("")]
    public void Constructor_RejectsInvalidStudentId(string studentId)
    {
        var act = () => new AdmissionHold(Guid.NewGuid(), studentId, CreatedAt, CreatedAt.AddMinutes(10));
        act.Should().Throw<ArgumentException>().WithMessage("*17-digit*");
    }

    [Fact]
    public void Constructor_RejectsExpiryNotAfterCreation()
    {
        var act = () => new AdmissionHold(Guid.NewGuid(), ValidStudentId, CreatedAt, CreatedAt);
        act.Should().Throw<ArgumentException>().WithMessage("*Expiration must be after creation*");
    }

    [Fact]
    public void IsExpired_TrueAtOrAfterDeadline()
    {
        var hold = NewHold(expiresAt: CreatedAt.AddMinutes(10));

        hold.IsExpired(CreatedAt.AddMinutes(10)).Should().BeTrue();
        hold.IsExpired(CreatedAt.AddMinutes(11)).Should().BeTrue();
        hold.IsExpired(CreatedAt.AddMinutes(9)).Should().BeFalse();
    }
}
