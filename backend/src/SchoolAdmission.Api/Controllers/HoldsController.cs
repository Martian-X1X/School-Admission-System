using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.DTOs;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("holds")]
public class HoldsController : ControllerBase
{
    private readonly HoldService _holds;

    public HoldsController(HoldService holds)
    {
        _holds = holds;
    }

    /// <summary>
    /// Returns hold details. Per the assignment, this endpoint does not
    /// require the X-Student-Id header.
    /// </summary>
    [HttpGet("{holdId:guid}")]
    public async Task<ActionResult<HoldDetailsResponse>> Get(
        Guid holdId,
        CancellationToken cancellationToken)
    {
        var hold = await _holds.GetHoldAsync(holdId, cancellationToken)
            ?? throw new NotFoundException("Hold", holdId);

        return Ok(new HoldDetailsResponse(
            hold.Id,
            hold.ClassId,
            hold.Status.ToApiString(),
            hold.ExpiresAt));
    }

    /// <summary>
    /// Confirms the admission (completes an existing hold). Idempotent.
    /// </summary>
    [HttpPost("{holdId:guid}/confirm")]
    public async Task<ActionResult<ConfirmHoldResponse>> Confirm(
        Guid holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken cancellationToken)
    {
        var hold = await _holds.ConfirmHoldAsync(holdId, studentId, cancellationToken);

        return Ok(new ConfirmHoldResponse(
            hold.Id,
            hold.Status.ToApiString()));
    }

    /// <summary>
    /// Cancels the reservation and releases the seat. Idempotent.
    /// </summary>
    [HttpDelete("{holdId:guid}")]
    public async Task<IActionResult> Cancel(
        Guid holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken cancellationToken)
    {
        await _holds.CancelHoldAsync(holdId, studentId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Lists the student's applications (current and previous).
    /// </summary>
    [HttpGet("~/me/holds")]
    public async Task<ActionResult<IReadOnlyList<StudentHoldResponse>>> MyHolds(
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken cancellationToken)
    {
        var records = await _holds.GetStudentHoldsAsync(studentId, cancellationToken);

        var responses = records
            .Select(record => new StudentHoldResponse(
                record.Hold.Id,
                record.Hold.ClassId,
                record.ClassName,
                record.Hold.Status.ToApiString(),
                record.Hold.ExpiresAt,
                record.Hold.CreatedAt))
            .ToList();

        return Ok(responses);
    }
}
