using FluentAssertions;
using SchoolAdmission.Application.Models;

namespace SchoolAdmission.UnitTests;

public class SeatSummaryTests
{
    [Fact]
    public void AvailableSeats_FollowsTheBusinessFormula()
    {
        // Example from the assignment: 120 total, 15 held, 80 confirmed → 25 available.
        var summary = new SeatSummary(TotalSeats: 120, HeldSeats: 15, ConfirmedSeats: 80);

        summary.AvailableSeats.Should().Be(25);
    }

    [Fact]
    public void AvailableSeats_NeverGoesNegative()
    {
        var summary = new SeatSummary(TotalSeats: 10, HeldSeats: 6, ConfirmedSeats: 6);

        summary.AvailableSeats.Should().Be(0, "a defensive clamp keeps UI display sane");
    }

    [Fact]
    public void AvailableSeats_DropsAsHoldsGrow()
    {
        var summary = new SeatSummary(TotalSeats: 10, HeldSeats: 0, ConfirmedSeats: 0);

        summary.AvailableSeats.Should().Be(10);

        summary = summary with { HeldSeats = 4 };
        summary.AvailableSeats.Should().Be(6);

        summary = summary with { ConfirmedSeats = 2 };
        summary.AvailableSeats.Should().Be(4);
    }
}
