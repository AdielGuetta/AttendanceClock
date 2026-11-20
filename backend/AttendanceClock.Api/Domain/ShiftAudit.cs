namespace AttendanceClock.Api.Domain;

/// <summary>
/// One permanent record of a manager changing a shift (PRD F6, D-11). Rows are only ever added, never edited,
/// so the history of every non-API time stays complete: who, when, what, old → new, and why.
/// </summary>
public class ShiftAudit
{
    public long Id { get; set; }

    public long ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    /// <summary>The manager who made the change.</summary>
    public int ChangedByEmployeeId { get; set; }
    public Employee ChangedBy { get; set; } = null!;

    /// <summary>When the change was made, from the trusted clock (not the server clock).</summary>
    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>"ClockIn" or "ClockOut".</summary>
    public required string Field { get; set; }

    /// <summary>ISO time with offset, or "(open)" when the shift had no Clock Out yet.</summary>
    public required string OldValue { get; set; }

    public required string NewValue { get; set; }

    public required string Reason { get; set; }
}
