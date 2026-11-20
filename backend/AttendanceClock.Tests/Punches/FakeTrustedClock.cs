using AttendanceClock.Api.Time;

namespace AttendanceClock.Tests.Punches;

/// <summary>
/// A clock the test controls: "it's now 08:00 in Zurich", or "the time service is down".
/// It exists ONLY in tests. The real app always uses the external time APIs.
/// </summary>
public sealed class FakeTrustedClock : ITrustedClock
{
    private TrustedTime? _now;
    private bool _down;

    public void SetNow(string isoWithOffset)
    {
        _now = new TrustedTime(DateTimeOffset.Parse(isoWithOffset), "fake-clock");
        _down = false;
    }

    public void SimulateOutage() => _down = true;

    public Task<TrustedTime> GetZurichNowAsync(CancellationToken cancellationToken = default) =>
        _down
            ? throw new TimeSourceUnavailableException(["timeapi.io: simulated outage", "time.now: simulated outage"])
            : Task.FromResult(_now ?? throw new InvalidOperationException("Test forgot to call SetNow()."));
}
