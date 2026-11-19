namespace AttendanceClock.Api.Domain;

/// <summary>
/// One shift = one Clock In and (later) one Clock Out.
/// While <see cref="ClockOut"/> is null, the shift is "open" (the employee is on shift).
/// </summary>
/// <remarks>
/// Times are <see cref="DateTimeOffset"/>: the exact moment plus the Zurich UTC offset at that moment
/// (+01:00 in winter, +02:00 in summer). One value keeps both the true instant (for correct durations
/// across daylight-saving changes) and the wall-clock time the employee saw. See PRD D-6.
/// </remarks>
public class Shift
{
    public long Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>Clock In time, from the external time API (Europe/Zurich).</summary>
    public DateTimeOffset ClockIn { get; set; }

    /// <summary>Where the Clock In time came from, e.g. the time provider's name, or "ManualCorrection".</summary>
    public required string ClockInSource { get; set; }

    /// <summary>Clock Out time; null while the shift is still open.</summary>
    public DateTimeOffset? ClockOut { get; set; }

    public string? ClockOutSource { get; set; }

    public ShiftFlags Flags { get; set; } = ShiftFlags.None;

    /// <summary>Changes automatically on every update. Used to detect two people editing the same shift at once.</summary>
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Reasons a manager should look at a shift. A shift can have several at once.</summary>
[Flags]
public enum ShiftFlags
{
    None = 0,
    ForgotClockOut = 1, // open for more than 14 hours
    TooLong = 2,        // closed, but longer than 14 hours
    Corrected = 4,      // a manager changed its times
}
