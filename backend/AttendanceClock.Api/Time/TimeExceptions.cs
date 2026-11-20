namespace AttendanceClock.Api.Time;

/// <summary>One provider gave an unusable answer (bad HTTP status, bad JSON, failed validation).</summary>
public sealed class TimeSourceException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Every provider failed. The punch must be rejected; the API returns 503 with code TIME_SOURCE_UNAVAILABLE.
/// </summary>
public sealed class TimeSourceUnavailableException(IReadOnlyList<string> failures)
    : Exception("No external time source gave a valid answer: " + string.Join(" | ", failures))
{
    public IReadOnlyList<string> Failures { get; } = failures;
}
