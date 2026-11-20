using AttendanceClock.Api.Auth;
using AttendanceClock.Api.Common;
using AttendanceClock.Api.Domain;
using AttendanceClock.Api.History;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceClock.Api.Admin;

/// <summary>Manager-only views and corrections (Plan 10). Employees get 403 Forbidden.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(EmployeeRole.Manager))]
public class AdminController(AdminService admin) : ControllerBase
{
    [HttpGet("employees")]
    public async Task<IActionResult> Employees(CancellationToken ct) => Ok(await admin.GetEmployeesAsync(ct));

    [HttpGet("on-shift")]
    public async Task<IActionResult> OnShift(CancellationToken ct) => Ok(await admin.GetOnShiftAsync(ct));

    [HttpGet("shifts")]
    public async Task<IActionResult> Shifts([FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] int? employeeId, [FromQuery] bool flaggedOnly, CancellationToken ct)
    {
        if (from > to || to.DayNumber - from.DayNumber + 1 > HistoryService.MaxRangeDays)
            return ApiProblem.Create(400, "INVALID_RANGE", "Invalid date range.",
                $"'from' must be on or before 'to', and the range at most {HistoryService.MaxRangeDays} days.");
        return Ok(await admin.GetShiftsAsync(from, to, employeeId, flaggedOnly, ct));
    }

    [HttpGet("shifts/{id:long}/audit")]
    public async Task<IActionResult> Audit(long id, CancellationToken ct) =>
        await admin.GetAuditAsync(id, ct) is { } audit ? Ok(audit) : NotFound();

    [HttpPatch("shifts/{id:long}")]
    public async Task<IActionResult> Correct(long id, CorrectShiftRequest request, CancellationToken ct)
    {
        var result = await admin.CorrectAsync(id, User.GetEmployeeId(), request, ct);
        return result.Error switch
        {
            CorrectionError.None => Ok(result.Shift),
            CorrectionError.NotFound => NotFound(),
            CorrectionError.ReasonRequired => Bad("REASON_REQUIRED", "A reason is required.",
                $"Explain the correction in at least {AdminService.MinReasonLength} characters."),
            CorrectionError.NothingToChange => Bad("NOTHING_TO_CHANGE", "Nothing to change.", "The new times are the same as the current ones."),
            CorrectionError.InvalidTimeFormat => Bad("INVALID_TIME_FORMAT", "Invalid time.", "Use Zurich time like 2026-11-20T17:00."),
            CorrectionError.AmbiguousTime => Bad("AMBIGUOUS_TIME", "This time happens twice.",
                "On the night clocks go back, 02:00–02:59 occurs twice. Add +02:00 (first) or +01:00 (second)."),
            CorrectionError.TimeDoesNotExist => Bad("INVALID_LOCAL_TIME", "This time doesn't exist.",
                "On the night clocks jump forward, 02:00–02:59 is skipped in Zurich."),
            CorrectionError.OffsetNotZurich => Bad("INVALID_LOCAL_TIME", "Wrong offset for Zurich.",
                "That offset isn't Zurich's for that date. Leave the offset out and the server will apply it."),
            CorrectionError.ClockOutNotAfterClockIn => Bad("CLOCK_OUT_NOT_AFTER_CLOCK_IN", "Clock Out must be after Clock In."),
            CorrectionError.TimeInFuture => Bad("TIME_IN_FUTURE", "Times can't be in the future."),
            CorrectionError.OverlapsAnotherShift => ApiProblem.Create(409, "OVERLAPS_ANOTHER_SHIFT",
                "This would overlap another shift of the same employee."),
            CorrectionError.ShiftModified => ApiProblem.Create(409, "SHIFT_MODIFIED",
                "This shift was changed since you opened it.", "Reload it and try again."),
            _ => StatusCode(500),
        };
    }

    private static ObjectResult Bad(string code, string title, string? detail = null) => ApiProblem.Create(400, code, title, detail);
}
