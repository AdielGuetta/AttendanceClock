using AttendanceClock.Api.Auth;
using AttendanceClock.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceClock.Api.History;

[ApiController]
[Route("api/shifts")]
[Authorize]
public class ShiftsController(HistoryService history) : ControllerBase
{
    /// <summary>
    /// GET /api/shifts/mine?from=2026-11-16&amp;to=2026-11-22: my shifts and totals for Zurich dates (inclusive).
    /// No dates → the current Zurich week. The employee comes from the login token, never from the URL.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        if (from is null != to is null)
            return InvalidRange("Send both 'from' and 'to', or neither (for the current week).");

        var (rangeFrom, rangeTo) = from is { } f && to is { } t ? (f, t) : await history.CurrentWeekAsync(ct);

        if (rangeFrom > rangeTo)
            return InvalidRange("'from' must be on or before 'to'.");
        if (rangeTo.DayNumber - rangeFrom.DayNumber + 1 > HistoryService.MaxRangeDays)
            return InvalidRange($"The range can be at most {HistoryService.MaxRangeDays} days.");

        return Ok(await history.GetAsync(User.GetEmployeeId(), rangeFrom, rangeTo, ct));
    }

    private static ObjectResult InvalidRange(string detail) =>
        ApiProblem.Create(StatusCodes.Status400BadRequest, "INVALID_RANGE", "Invalid date range.", detail);
}
