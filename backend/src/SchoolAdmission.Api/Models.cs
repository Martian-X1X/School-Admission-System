namespace SchoolAdmission.Api;

public sealed record AdmissionOptions(int HoldDurationSeconds, int ExpirySweepSeconds);

public sealed class CreateClassRequest
{
    public string? Name { get; set; }
    public int? TotalSeats { get; set; }
}

public sealed class ClassCreated
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
}

public sealed class ClassSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public int HeldSeats { get; set; }
    public int ConfirmedSeats { get; set; }
    public int AvailableSeats => Math.Max(0, TotalSeats - HeldSeats - ConfirmedSeats);
}

public sealed class HoldCreated
{
    public Guid HoldId { get; set; }
    public Guid ClassId { get; set; }
    public string StudentId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public sealed class HoldView
{
    public Guid HoldId { get; set; }
    public Guid ClassId { get; set; }
    public string Status { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public sealed class HoldConfirmed
{
    public Guid HoldId { get; set; }
    public string Status { get; set; } = "";
}

public sealed class MyHold
{
    public Guid HoldId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class LockedHold
{
    public Guid Id { get; set; }
    public string StudentId { get; set; } = "";
    public string Status { get; set; } = "";
}
