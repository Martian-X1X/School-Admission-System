namespace SchoolAdmission.Domain.Entities;

public class AdmissionClass
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = "";

    public int TotalSeats { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AdmissionClass() { }

    public AdmissionClass(string name, int totalSeats)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Class name is required.", nameof(name));

        if (totalSeats <= 0)
            throw new ArgumentException("Total seats must be positive.", nameof(totalSeats));

        Id = Guid.NewGuid();
        Name = name.Trim();
        TotalSeats = totalSeats;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Rebuilds an existing class (as read from persistence) without generating new identity.
    /// </summary>
    public static AdmissionClass Restore(
        Guid id,
        string name,
        int totalSeats,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = id,
            Name = name,
            TotalSeats = totalSeats,
            CreatedAt = createdAt
        };
}
