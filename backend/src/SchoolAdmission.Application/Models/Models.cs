using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Models;

/// <summary>
/// Seat statistics for a class. Held counts only non-expired HELD applications.
/// </summary>
public sealed record SeatSummary(int TotalSeats, int HeldSeats, int ConfirmedSeats)
{
    public int AvailableSeats => Math.Max(0, TotalSeats - HeldSeats - ConfirmedSeats);
}

public sealed record ClassWithSeats(AdmissionClass Class, SeatSummary Seats);

public sealed record StudentHoldRecord(AdmissionHold Hold, string ClassName);
