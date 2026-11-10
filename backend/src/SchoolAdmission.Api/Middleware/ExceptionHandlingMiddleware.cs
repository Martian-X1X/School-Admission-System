using System.Text.Json;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Domain.Exceptions;

namespace SchoolAdmission.Api.Middleware;

/// <summary>
/// Translates application and domain exceptions into RFC 7807 Problem Details
/// responses. Raw database errors and stack traces are never leaked to clients.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const string ErrorTypeBase = "https://school-admission.errors/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail, errorType) = exception switch
        {
            AppValidationException e => (
                StatusCodes.Status400BadRequest, "Invalid request", e.Message, "validation"),
            NotFoundException e => (
                StatusCodes.Status404NotFound, "Resource not found", e.Message, "not-found"),
            OwnershipException e => (
                StatusCodes.Status403Forbidden, "Forbidden", e.Message, "ownership"),
            SeatUnavailableException e => (
                StatusCodes.Status409Conflict, "No seats available", e.Message, "seat-unavailable"),
            DuplicateApplicationException e => (
                StatusCodes.Status409Conflict, "Duplicate application", e.Message, "duplicate-application"),
            HoldExpiredException e => (
                StatusCodes.Status409Conflict, "Hold expired", e.Message, "hold-expired"),
            InvalidHoldTransitionException e => (
                StatusCodes.Status409Conflict, "Invalid state transition", e.Message, "invalid-transition"),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (
                499, "Client closed request", "The request was cancelled by the client.", "client-closed-request"),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An unexpected error occurred. Please try again later.",
                "unexpected-error")
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("{ErrorType} at {Method} {Path}: {Detail}",
                errorType, context.Request.Method, context.Request.Path, detail);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDocument(
            $"{ErrorTypeBase}{errorType}",
            title,
            statusCode,
            detail);

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private sealed record ProblemDocument(string Type, string Title, int Status, string Detail);
}
