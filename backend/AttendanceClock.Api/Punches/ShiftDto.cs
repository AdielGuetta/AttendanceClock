using AttendanceClock.Api.Domain;

namespace AttendanceClock.Api.Punches;

/// <summary>A shift as the UI sees it. Times carry the Zurich offset, e.g. "2026-11-20T08:02:11+01:00".</summary>
public sealed record ShiftDto(
    long Id,
    DateTimeOffset ClockIn,
    string ClockInSource,
    DateTimeOffset? ClockOut,
    string? ClockOutSource,
    int? DurationMinutes,
    IReadOnlyList<string> Flags)
{
    public static ShiftDto From(Shift s) => new(
        s.Id,
        s.ClockIn,
        s.ClockInSource,
        s.ClockOut,
        s.ClockOutSource,
        // Subtracting DateTimeOffsets compares the real instants, so DST nights come out right (PRD D-6).
        s.ClockOut is { } clockOut ? (int)(clockOut - s.ClockIn).TotalMinutes : null,
        Enum.GetValues<ShiftFlags>().Where(f => f != ShiftFlags.None && s.Flags.HasFlag(f)).Select(f => f.ToString()).ToList());
}

public sealed record PunchStatusDto(string State, ShiftDto? OpenShift);
