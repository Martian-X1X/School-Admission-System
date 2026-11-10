namespace SchoolAdmission.Application.DTOs;

/// <summary>
/// API request/response contracts. Serialized camelCase; response shapes match the
/// assignment specification exactly and are never wrapped in an envelope.
/// </summary>
public sealed record CreateClassRequest(string? Name, int? TotalSeats);

public sealed record ClassResponse(
    Guid Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);

public sealed record CreateHoldResponse(
    Guid HoldId,
    Guid ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record HoldDetailsResponse(
    Guid HoldId,
    Guid ClassId,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record ConfirmHoldResponse(
    Guid HoldId,
    string Status);

public sealed record StudentHoldResponse(
    Guid HoldId,
    Guid ClassId,
    string ClassName,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
