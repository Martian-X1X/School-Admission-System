using SchoolAdmission.Application.DTOs;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Interfaces;
using SchoolAdmission.Application.Models;
using SchoolAdmission.Application.Validators;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Services;

/// <summary>
/// Class Management module: create admission classes and expose seat statistics.
/// </summary>
public class ClassService
{
    private readonly IClassRepository _classes;

    public ClassService(IClassRepository classes)
    {
        _classes = classes;
    }

    public async Task<ClassResponse> CreateAsync(CreateClassRequest request, CancellationToken cancellationToken = default)
    {
        var entity = CreateClassValidator.ValidateAndCreate(request.Name, request.TotalSeats);
        var saved = await _classes.AddAsync(entity, cancellationToken);
        return Map(saved, new SeatSummary(saved.TotalSeats, 0, 0));
    }

    public async Task<ClassResponse?> GetByIdAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        var found = await _classes.FindAsync(classId, cancellationToken);
        return found is null ? null : Map(found.Class, found.Seats);
    }

    public async Task<IReadOnlyList<ClassResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var classes = await _classes.ListAsync(cancellationToken);
        return classes.Select(c => Map(c.Class, c.Seats)).ToList();
    }

    public async Task<ClassResponse> GetRequiredAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(classId, cancellationToken)
            ?? throw new NotFoundException("Class", classId);
    }

    private static ClassResponse Map(AdmissionClass admissionClass, SeatSummary seats) =>
        new(
            admissionClass.Id,
            admissionClass.Name,
            admissionClass.TotalSeats,
            seats.HeldSeats,
            seats.ConfirmedSeats,
            seats.AvailableSeats);
}
