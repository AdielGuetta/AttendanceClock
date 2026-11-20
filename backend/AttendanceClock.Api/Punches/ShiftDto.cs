using AttendanceClock.Api.Domain;
using AttendanceClock.Api.Time;

namespace AttendanceClock.Api.Punches;

/// <summary>
/// A shift as the UI sees it. Times carry the Zurich offset, e.g. "2026-11-20T08:02:11+01:00".
/// <see cref="ZurichDate"/> is the calendar day the shift belongs to: the Zurich date of its Clock In (PRD D-10).
/// </summary>
public sealed record ShiftDto(
    long Id,
    DateOnly ZurichDate,
    DateTimeOffset ClockIn,
    string ClockInSource,
    DateTimeOffset? ClockOut,
    string? ClockOutSource,
    int? DurationMinutes,
    IReadOnlyList<string> Flags)
{
    public static ShiftDto From(Shift s) => new(
        s.Id,
        ZurichTime.ToZurichDate(s.ClockIn),
        s.ClockIn,
        s.ClockInSource,
        s.ClockOut,
        s.ClockOutSource,
        // Subtracting DateTimeOffsets compares the real instants, so DST nights come out right (PRD D-6).
        s.ClockOut is { } clockOut ? (int)(clockOut - s.ClockIn).TotalMinutes : null,
        Enum.GetValues<ShiftFlags>().Where(f => f != ShiftFlags.None && s.Flags.HasFlag(f)).Select(f => f.ToString()).ToList());
}

public sealed record PunchStatusDto(string State, ShiftDto? OpenShift);
