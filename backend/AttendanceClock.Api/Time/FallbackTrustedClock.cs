using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace AttendanceClock.Api.Time;

/// <summary>
/// Asks the external time sources in order (primary first). The first valid answer wins.
/// If every source fails, throws <see cref="TimeSourceUnavailableException"/>. It never falls back to the
/// local clock (PRD D-5: a wrong time is worse than a rejected punch).
/// </summary>
public sealed class FallbackTrustedClock(
    IEnumerable<IExternalTimeSource> sources,
    IOptions<TimeProviderOptions> options,
    ILogger<FallbackTrustedClock> logger) : ITrustedClock
{
    private readonly IReadOnlyList<IExternalTimeSource> _sources = sources.ToList();

    public async Task<TrustedTime> GetZurichNowAsync(CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        foreach (var source in _sources)
        {
            // Each provider gets its own time budget, so a slow primary can't eat the backup's time.
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attempt.CancelAfter(options.Value.AttemptTimeout);

            // Stopwatch measures elapsed time for the log. It doesn't read the time of day.
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var value = await source.GetZurichTimeAsync(attempt.Token);
                ZurichTime.Validate(value, source.Name);

                logger.LogInformation("Time from {Source}: {Time:O} in {ElapsedMs} ms",
                    source.Name, value, stopwatch.ElapsedMilliseconds);
                return new TrustedTime(value, source.Name);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add($"{source.Name}: timed out after {options.Value.AttemptTimeout.TotalSeconds:0.#} s");
            }
            catch (Exception ex) when (ex is TimeSourceException or HttpRequestException)
            {
                failures.Add(ex.Message.StartsWith(source.Name) ? ex.Message : $"{source.Name}: {ex.Message}");
            }

            logger.LogWarning("Time source {Source} failed after {ElapsedMs} ms: {Failure}",
                source.Name, stopwatch.ElapsedMilliseconds, failures[^1]);
        }

        logger.LogError("All time sources failed: {Failures}", string.Join(" | ", failures));
        throw new TimeSourceUnavailableException(failures);
    }
}
