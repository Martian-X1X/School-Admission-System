using FluentAssertions;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.UnitTests;

public class AdmissionClassTests
{
    [Fact]
    public void Constructor_AcceptsValidInput()
    {
        var admissionClass = new AdmissionClass("Class 6 - Morning Shift", 120);

        admissionClass.Id.Should().NotBeEmpty();
        admissionClass.Name.Should().Be("Class 6 - Morning Shift");
        admissionClass.TotalSeats.Should().Be(120);
        admissionClass.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var admissionClass = new AdmissionClass("  Class 6  ", 30);

        admissionClass.Name.Should().Be("Class 6");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingName(string? name)
    {
        var act = () => new AdmissionClass(name!, 120);
        act.Should().Throw<ArgumentException>().WithMessage("*Class name is required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_RejectsNonPositiveSeatCount(int totalSeats)
    {
        var act = () => new AdmissionClass("Class 6", totalSeats);
        act.Should().Throw<ArgumentException>().WithMessage("*Total seats must be positive*");
    }

    [Fact]
    public void Restore_RebuildsEntityWithOriginalIdentity()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);

        var restored = AdmissionClass.Restore(id, "Class 6", 45, createdAt);

        restored.Id.Should().Be(id);
        restored.Name.Should().Be("Class 6");
        restored.TotalSeats.Should().Be(45);
        restored.CreatedAt.Should().Be(createdAt);
    }
}
