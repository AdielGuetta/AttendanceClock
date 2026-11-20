using AttendanceClock.Api.Data;
using AttendanceClock.Api.Punches;
using AttendanceClock.Api.Time;
using Microsoft.EntityFrameworkCore;

namespace AttendanceClock.Api.History;

/// <summary>An employee's shifts for a range of Zurich calendar dates, with daily and range totals (Plan 08, PRD F4).</summary>
public sealed class HistoryService(AppDbContext db, ITrustedClock clock)
{
    /// <summary>Longest range one request may ask for (about three months).</summary>
    public const int MaxRangeDays = 93;

    /// <summary>
    /// The current Zurich week, Monday to Sunday. Uses the trusted clock to know "today",
    /// because the browser's or server's clock can be wrong.
    /// </summary>
    public async Task<(DateOnly From, DateOnly To)> CurrentWeekAsync(CancellationToken ct)
    {
        var today = ZurichTime.ToZurichDate((await clock.GetZurichNowAsync(ct)).Value);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(6));
    }

    public async Task<HistoryDto> GetAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        // Day boundaries come from Zurich's official rules, so DST days (23 h / 25 h) are cut correctly.
        var start = ZurichTime.StartOfDay(from);
        var end = ZurichTime.StartOfDay(to.AddDays(1));

        // A shift belongs to the date of its Clock In (D-10): filter on ClockIn only.
        var closed = await db.Shifts.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId && s.ClockOut != null && s.ClockIn >= start && s.ClockIn < end)
            .OrderBy(s => s.ClockIn)
            .ToListAsync(ct);

        var open = await db.Shifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.ClockOut == null, ct);

        // Totals add the EXACT durations first and round down only once, so many short shifts don't
        // lose time to per-shift rounding (e.g. 3 × 1 min 40 s = 5 min, not 3).
        static int WholeMinutes(IEnumerable<Domain.Shift> s) =>
            (int)TimeSpan.FromTicks(s.Sum(x => (x.ClockOut!.Value - x.ClockIn).Ticks)).TotalMinutes;

        var days = closed
            .GroupBy(s => ZurichTime.ToZurichDate(s.ClockIn))
            .Select(g => new DayTotalDto(g.Key, WholeMinutes(g), g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        return new HistoryDto(from, to, WholeMinutes(closed), days, closed.Select(ShiftDto.From).ToList(),
            open is null ? null : ShiftDto.From(open));
    }
}

/// <param name="TotalMinutes">Closed shifts only. The open shift's length isn't known yet.</param>
public sealed record HistoryDto(
    DateOnly From,
    DateOnly To,
    int TotalMinutes,
    IReadOnlyList<DayTotalDto> Days,
    IReadOnlyList<ShiftDto> Shifts,
    ShiftDto? OpenShift);

public sealed record DayTotalDto(DateOnly Date, int TotalMinutes, int ShiftCount);
