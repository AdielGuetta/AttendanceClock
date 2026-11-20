namespace AttendanceClock.Api.Time;

/// <summary>
/// One external time API. (Deliberately not called "ITimeProvider", to avoid confusion with .NET's
/// built-in <see cref="System.TimeProvider"/>, which reads the local clock.)
/// </summary>
public interface IExternalTimeSource
{
    /// <summary>Short provider name, stored with each punch (max 32 chars).</summary>
    string Name { get; }

    /// <summary>Calls the provider and returns Zurich time with its offset.</summary>
    /// <exception cref="TimeSourceException">The answer was missing, malformed or failed validation.</exception>
    Task<DateTimeOffset> GetZurichTimeAsync(CancellationToken cancellationToken);
}
