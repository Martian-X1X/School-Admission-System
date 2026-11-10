using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Validators;

public static class CreateClassValidator
{
    private const int MaxNameLength = 200;

    public static AdmissionClass ValidateAndCreate(string? name, int? totalSeats)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Class name is required.");
        }
        else if (name.Trim().Length > MaxNameLength)
        {
            errors.Add($"Class name must be {MaxNameLength} characters or fewer.");
        }

        if (totalSeats is null)
        {
            errors.Add("Total seats is required.");
        }
        else if (totalSeats <= 0)
        {
            errors.Add("Total seats must be a positive number.");
        }

        if (errors.Count > 0)
        {
            throw new AppValidationException(string.Join(" ", errors));
        }

        return new AdmissionClass(name!.Trim(), totalSeats!.Value);
    }
}
