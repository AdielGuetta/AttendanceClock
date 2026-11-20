using System.Net;
using System.Text;

namespace AttendanceClock.Tests.Time;

/// <summary>
/// Stands in for the internet: returns whatever answer a test wants, or hangs to simulate a timeout.
/// No real network calls are made, so tests are fast and repeatable.
/// </summary>
public sealed class FakeHttpHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return respond(cancellationToken);
    }

    public static FakeHttpHandler Json(string json) =>
        new(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));

    public static FakeHttpHandler Status(HttpStatusCode status) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status)));

    /// <summary>Never answers, until the caller gives up (cancels).</summary>
    public static FakeHttpHandler Hangs() =>
        new(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });
}

/// <summary>Realistic answers, in each provider's real format (copied from the Plan 04 spike).</summary>
public static class ProviderJson
{
    public static string TimeApiIo(string localTime, int offsetSeconds, string zone = "Europe/Zurich") => $$"""
        { "timeZone": "{{zone}}", "currentLocalTime": "{{localTime}}",
          "currentUtcOffset": { "seconds": {{offsetSeconds}} }, "isDayLightSavingActive": false }
        """;

    public static string TimeNow(string datetime, string utcDatetime, string zone = "Europe/Zurich") => $$"""
        { "abbreviation": "CET", "timezone": "{{zone}}", "datetime": "{{datetime}}",
          "utc_datetime": "{{utcDatetime}}", "utc_offset": "+01:00", "dst": false }
        """;
}
