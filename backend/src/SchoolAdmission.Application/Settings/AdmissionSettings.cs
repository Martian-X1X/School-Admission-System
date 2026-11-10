using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Application.Settings;

public class AdmissionSettings
{
    public const string SectionName = "Admission";

    /// <summary>
    /// How long a seat stays held before it is automatically released.
    /// Also settable through the HOLD_DURATION_SECONDS environment variable.
    /// </summary>
    [Range(1, 86400, ErrorMessage = "HoldDurationSeconds must be between 1 and 86400.")]
    public int HoldDurationSeconds { get; set; } = 600;
}
