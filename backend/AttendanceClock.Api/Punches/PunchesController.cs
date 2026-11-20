using AttendanceClock.Api.Auth;
using AttendanceClock.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceClock.Api.Punches;

/// <summary>
/// Clock In / Clock Out / status for the logged-in employee (Plan 07).
/// No request body: the client never sends a time; the server gets it from the trusted clock (PRD D-2).
/// </summary>
[ApiController]
[Route("api/punches")]
[Authorize] // employees and managers alike
public class PunchesController(ShiftService shifts) : ControllerBase
{
    [HttpPost("clock-in")]
    public async Task<IActionResult> ClockIn(CancellationToken ct)
    {
        var employeeId = User.GetEmployeeId();
        if (!await shifts.IsActiveEmployeeAsync(employeeId, ct))
            return InactiveAccount();

        var result = await shifts.ClockInAsync(employeeId, ct);
        return result.Outcome switch
        {
            PunchOutcome.Ok => StatusCode(StatusCodes.Status201Created, ShiftDto.From(result.Shift!)),
            PunchOutcome.AlreadyOnShift => ApiProblem.Create(StatusCodes.Status409Conflict, "ALREADY_ON_SHIFT",
                "You're already on shift.",
                result.Shift is { } open ? $"You've been on shift since {open.ClockIn:yyyy-MM-dd HH:mm} (Zurich time). Clock out first." : null,
                new Dictionary<string, object?> { ["openShift"] = result.Shift is { } s ? ShiftDto.From(s) : null }),
            _ => TimeInconsistent(),
        };
    }

    [HttpPost("clock-out")]
    public async Task<IActionResult> ClockOut(CancellationToken ct)
    {
        var employeeId = User.GetEmployeeId();
        if (!await shifts.IsActiveEmployeeAsync(employeeId, ct))
            return InactiveAccount();

        var result = await shifts.ClockOutAsync(employeeId, ct);
        return result.Outcome switch
        {
            PunchOutcome.Ok => Ok(ShiftDto.From(result.Shift!)),
            PunchOutcome.NotOnShift => ApiProblem.Create(StatusCodes.Status409Conflict, "NOT_ON_SHIFT",
                "You're not on shift.", "There's no open shift to clock out of. Refresh to see your current status."),
            _ => TimeInconsistent(),
        };
    }

    /// <summary>Am I on shift? Doesn't call the time API, by design (Plan 07: avoids extra external calls, EC-17).</summary>
    [HttpGet("status")]
    public async Task<ActionResult<PunchStatusDto>> Status(CancellationToken ct)
    {
        var open = await shifts.GetOpenShiftAsync(User.GetEmployeeId(), ct);
        return open is null
            ? new PunchStatusDto("OffShift", null)
            : new PunchStatusDto("OnShift", ShiftDto.From(open));
    }

    private static ObjectResult InactiveAccount() => ApiProblem.Create(StatusCodes.Status401Unauthorized,
        "ACCOUNT_INACTIVE", "Your account is no longer active.");

    private static ObjectResult TimeInconsistent() => ApiProblem.Create(StatusCodes.Status503ServiceUnavailable,
        "TIME_SOURCE_INCONSISTENT", "The time service returned an inconsistent time.",
        "Nothing was recorded. Please try again in a moment.");
}
