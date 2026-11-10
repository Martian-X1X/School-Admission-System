using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Infrastructure.BackgroundServices;

public class ExpirationSettings
{
    public const string SectionName = "Expiration";

    [Range(1, 3600, ErrorMessage = "ScanIntervalSeconds must be between 1 and 3600.")]
    public int ScanIntervalSeconds { get; set; } = 5;

    [Range(1, 10000, ErrorMessage = "BatchSize must be between 1 and 10000.")]
    public int BatchSize { get; set; } = 500;
}
