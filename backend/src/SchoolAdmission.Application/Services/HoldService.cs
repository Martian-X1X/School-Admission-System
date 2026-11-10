using Microsoft.Extensions.Options;
using SchoolAdmission.Application.Interfaces;
using SchoolAdmission.Application.Models;
using SchoolAdmission.Application.Settings;
using SchoolAdmission.Application.Validators;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Services;

/// <summary>
/// Admission Application / Confirmation / Tracking module: reserve seats,
/// confirm or cancel holds, and track a student's applications.
/// </summary>
public class HoldService
{
    private readonly IHoldRepository _holds;
    private readonly AdmissionSettings _settings;

    public HoldService(IHoldRepository holds, IOptions<AdmissionSettings> settings)
    {
        _holds = holds;
        _settings = settings.Value;
    }

    public Task<AdmissionHold> CreateHoldAsync(Guid classId, string? studentId, CancellationToken cancellationToken = default)
    {
        var validated = StudentIdValidator.EnsureValid(studentId);
        var duration = TimeSpan.FromSeconds(_settings.HoldDurationSeconds);
        return _holds.CreateHoldAsync(classId, validated, duration, cancellationToken);
    }

    public Task<AdmissionHold?> GetHoldAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        return _holds.FindByIdAsync(holdId, cancellationToken);
    }

    public Task<AdmissionHold> ConfirmHoldAsync(Guid holdId, string? studentId, CancellationToken cancellationToken = default)
    {
        var validated = StudentIdValidator.EnsureValid(studentId);
        return _holds.ConfirmAsync(holdId, validated, cancellationToken);
    }

    public Task<AdmissionHold> CancelHoldAsync(Guid holdId, string? studentId, CancellationToken cancellationToken = default)
    {
        var validated = StudentIdValidator.EnsureValid(studentId);
        return _holds.CancelAsync(holdId, validated, cancellationToken);
    }

    public Task<IReadOnlyList<StudentHoldRecord>> GetStudentHoldsAsync(string? studentId, CancellationToken cancellationToken = default)
    {
        var validated = StudentIdValidator.EnsureValid(studentId);
        return _holds.GetByStudentIdAsync(validated, cancellationToken);
    }
}
