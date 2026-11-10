using FluentAssertions;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Domain.Exceptions;

namespace SchoolAdmission.UnitTests;

/// <summary>
/// Verifies the hold state machine:
///
///   HELD ──confirm──► CONFIRMED
///     ├──cancel──► CANCELLED
///     └──expiry──► EXPIRED
///
/// EXPIRED → CONFIRMED, CANCELLED → CONFIRMED and CONFIRMED → CANCELLED are forbidden.
/// </summary>
public class HoldStateMachineTests
{
    private static readonly string StudentId = "20152691234567890";

    private static AdmissionHold Hold(DateTimeOffset? expiresAt = null)
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        return new AdmissionHold(Guid.NewGuid(), StudentId, createdAt, expiresAt ?? createdAt.AddMinutes(10));
    }

    private static AdmissionHold Restored(HoldStatus status) =>
        AdmissionHold.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StudentId,
            status,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5),
            status == HoldStatus.Confirmed ? DateTimeOffset.UtcNow : null);

    [Fact]
    public void Confirm_BeforeExpiry_TransitionsToConfirmed()
    {
        var hold = Hold();
        var now = DateTimeOffset.UtcNow;

        hold.Confirm(now);

        hold.Status.Should().Be(HoldStatus.Confirmed);
        hold.ConfirmedAt.Should().Be(now);
    }

    [Fact]
    public void Confirm_IsIdempotent_DoesNotChangeConfirmedAt()
    {
        var hold = Hold();
        var first = DateTimeOffset.UtcNow;

        hold.Confirm(first);
        hold.Confirm(first.AddMinutes(1));

        hold.Status.Should().Be(HoldStatus.Confirmed);
        hold.ConfirmedAt.Should().Be(first, "repeated confirmation must not alter the original confirmation");
    }

    [Fact]
    public void Confirm_AfterExpiry_FlipsToExpiredAndThrows()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var hold = Hold(expiresAt);

        var act = () => hold.Confirm(DateTimeOffset.UtcNow);

        act.Should().Throw<HoldExpiredException>();
        hold.Status.Should().Be(HoldStatus.Expired, "the domain method must persist the expiry flip");
    }

    [Theory]
    [InlineData(HoldStatus.Cancelled)]
    [InlineData(HoldStatus.Expired)]
    public void Confirm_FromTerminalStates_IsRejected(HoldStatus status)
    {
        var hold = Restored(status);

        var act = () => hold.Confirm(DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidHoldTransitionException>()
            .Which.To.Should().Be(HoldStatus.Confirmed);
        hold.Status.Should().Be(status, "terminal statuses must be stable");
    }

    [Fact]
    public void Cancel_FromHeld_TransitionsToCancelled()
    {
        var hold = Hold();

        hold.Cancel();

        hold.Status.Should().Be(HoldStatus.Cancelled);
    }

    [Fact]
    public void Cancel_IsIdempotent()
    {
        var hold = Hold();

        hold.Cancel();
        hold.Cancel();

        hold.Status.Should().Be(HoldStatus.Cancelled);
    }

    [Theory]
    [InlineData(HoldStatus.Confirmed)]
    [InlineData(HoldStatus.Expired)]
    public void Cancel_FromNonHeldStates_IsRejected(HoldStatus status)
    {
        var hold = Restored(status);

        var act = () => hold.Cancel();

        act.Should().Throw<InvalidHoldTransitionException>()
            .Which.To.Should().Be(HoldStatus.Cancelled);
        hold.Status.Should().Be(status);
    }

    [Fact]
    public void MarkExpired_FromHeld_ReturnsTrue()
    {
        var hold = Hold();

        hold.MarkExpired().Should().BeTrue();
        hold.Status.Should().Be(HoldStatus.Expired);
    }

    [Theory]
    [InlineData(HoldStatus.Confirmed)]
    [InlineData(HoldStatus.Expired)]
    [InlineData(HoldStatus.Cancelled)]
    public void MarkExpired_FromOtherStates_ReturnsFalse(HoldStatus status)
    {
        var hold = Restored(status);

        hold.MarkExpired().Should().BeFalse();
        hold.Status.Should().Be(status);
    }
}
