using System.Text.Json;
using System.Text.RegularExpressions;

namespace SchoolAdmission.Api;

public static class Helpers
{
    private static readonly Regex StudentIdRegex = new("^[0-9]{17}$", RegexOptions.Compiled);

    public static async Task<T> ReadBodyAsync<T>(HttpRequest req, CancellationToken ct) where T : class
    {
        try
        {
            var value = await req.ReadFromJsonAsync<T>(ct);
            return value ?? throw ApiException.Validation("Request body is required.");
        }
        catch (JsonException)
        {
            throw ApiException.Validation("Request body is not valid JSON.");
        }
        catch (InvalidOperationException)
        {
            throw ApiException.Validation("Send a JSON body with Content-Type: application/json.");
        }
    }

    public static string RequireStudentId(HttpRequest req)
    {
        var value = req.Headers["X-Student-Id"].ToString().Trim();
        if (!StudentIdRegex.IsMatch(value))
            throw new ApiException(400, "INVALID_STUDENT_ID",
                "X-Student-Id header must be a 17-digit birth registration number.");
        return value;
    }

    public static Guid ParseId(string raw, string what) =>
        Guid.TryParse(raw, out var id) ? id : throw ApiException.NotFound($"{what} not found.");
}
