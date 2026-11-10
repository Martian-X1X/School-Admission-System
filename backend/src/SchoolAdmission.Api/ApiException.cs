namespace SchoolAdmission.Api;

/// <summary>An expected, client-caused error. Always mapped to a 4xx response.</summary>
public sealed class ApiException : Exception
{
    public int Status { get; }
    public string Code { get; }

    public ApiException(int status, string code, string detail) : base(detail)
    {
        Status = status;
        Code = code;
    }

    public static ApiException Validation(string detail) => new(400, "VALIDATION", detail);
    public static ApiException NotFound(string detail) => new(404, "NOT_FOUND", detail);
    public static ApiException Conflict(string code, string detail) => new(409, code, detail);
    public static ApiException Forbidden(string detail) => new(403, "NOT_OWNER", detail);
}
