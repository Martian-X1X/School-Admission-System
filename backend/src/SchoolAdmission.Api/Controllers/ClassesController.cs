using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.DTOs;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly ClassService _classes;
    private readonly HoldService _holds;

    public ClassesController(ClassService classes, HoldService holds)
    {
        _classes = classes;
        _holds = holds;
    }

    /// <summary>
    /// Creates an admission class. (School administrator operation.)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ClassResponse>> Create(
        [FromBody] CreateClassRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _classes.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { classId = created.Id }, created);
    }

    /// <summary>
    /// Lists admission classes with current seat statistics.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClassResponse>>> List(
        CancellationToken cancellationToken)
    {
        var classes = await _classes.ListAsync(cancellationToken);
        return Ok(classes);
    }

    /// <summary>
    /// Returns one admission class with its seat statistics.
    /// </summary>
    [HttpGet("{classId:guid}")]
    public async Task<ActionResult<ClassResponse>> GetById(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var found = await _classes.GetByIdAsync(classId, cancellationToken);
        return found is not null
            ? Ok(found)
            : throw new NotFoundException("Class", classId);
    }

    /// <summary>
    /// Reserves one seat (creates a temporary hold) for the identified student.
    /// </summary>
    [HttpPost("{classId:guid}/holds")]
    public async Task<ActionResult<CreateHoldResponse>> CreateHold(
        Guid classId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken cancellationToken)
    {
        var hold = await _holds.CreateHoldAsync(classId, studentId, cancellationToken);

        var response = new CreateHoldResponse(
            hold.Id,
            hold.ClassId,
            hold.StudentId,
            hold.Status.ToApiString(),
            hold.ExpiresAt);

        return CreatedAtAction("Get", "Holds", new { holdId = hold.Id }, response);
    }
}
