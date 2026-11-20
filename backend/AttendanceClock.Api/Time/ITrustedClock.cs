namespace AttendanceClock.Api.Time;

/// <summary>
/// The ONLY way the app gets "now" for attendance records (PRD D-3).
/// Implementations must get the time from an external time API. Never from the local clock.
/// </summary>
public interface ITrustedClock
{
    /// <summary>Current Zurich time from an external provider.</summary>
    /// <exception cref="TimeSourceUnavailableException">No provider gave a valid answer (fail closed, PRD D-5).</exception>
    Task<TrustedTime> GetZurichNowAsync(CancellationToken cancellationToken = default);
}

/// <param name="Value">The instant, carrying Zurich's UTC offset at that instant (+01:00 or +02:00).</param>
/// <param name="Source">Which provider answered, e.g. "timeapi.io". Stored next to each punch.</param>
public sealed record TrustedTime(DateTimeOffset Value, string Source);
