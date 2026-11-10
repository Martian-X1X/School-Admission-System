using SchoolAdmission.Application.Models;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Interfaces;

public interface IClassRepository
{
    Task<AdmissionClass> AddAsync(AdmissionClass admissionClass, CancellationToken cancellationToken = default);

    Task<ClassWithSeats?> FindAsync(Guid classId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassWithSeats>> ListAsync(CancellationToken cancellationToken = default);
}
