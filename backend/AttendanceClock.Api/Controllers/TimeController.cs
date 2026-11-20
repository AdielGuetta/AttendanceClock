using AttendanceClock.Api.Time;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceClock.Api.Controllers;

/// <summary>Current trusted Zurich time, for display (the live clock in the UI, PRD §4.5).</summary>
[ApiController]
[Route("api/time")]
public class TimeController(ITrustedClock clock) : ControllerBase
{
    /// <summary>GET /api/time/now → { "zurichTime": "2026-11-20T09:15:02.123+01:00", "source": "timeapi.io" }</summary>
    [HttpGet("now")]
    public async Task<ActionResult<TimeNowResponse>> GetNow(CancellationToken cancellationToken)
    {
        var now = await clock.GetZurichNowAsync(cancellationToken);
        return new TimeNowResponse(now.Value, now.Source);
    }
}

public sealed record TimeNowResponse(DateTimeOffset ZurichTime, string Source);
