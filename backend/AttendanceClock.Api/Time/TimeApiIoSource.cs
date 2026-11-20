using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AttendanceClock.Api.Time;

/// <summary>
/// Primary provider: timeapi.io (PRD D-4).
/// Example answer: { "timeZone": "Europe/Zurich", "currentLocalTime": "2026-11-19T20:15:40.4688",
///                   "currentUtcOffset": { "seconds": 3600 }, ... }
/// </summary>
public sealed class TimeApiIoSource(HttpClient http, IOptions<TimeProviderOptions> options) : IExternalTimeSource
{
    public string Name => "timeapi.io";

    public async Task<DateTimeOffset> GetZurichTimeAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(options.Value.TimeApiIoUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new TimeSourceException($"{Name}: HTTP {(int)response.StatusCode}.");

        Payload? body;
        try { body = await response.Content.ReadFromJsonAsync<Payload>(cancellationToken); }
        catch (System.Text.Json.JsonException ex) { throw new TimeSourceException($"{Name}: invalid JSON.", ex); }

        if (body?.TimeZone is null || body.CurrentLocalTime is null || body.CurrentUtcOffset is null)
            throw new TimeSourceException($"{Name}: response is missing required fields.");
        if (body.TimeZone != ZurichTime.TimeZoneId)
            throw new TimeSourceException($"{Name}: returned time zone '{body.TimeZone}', expected {ZurichTime.TimeZoneId}.");

        var local = ZurichTime.ParseLocal(body.CurrentLocalTime, Name);
        var offset = TimeSpan.FromSeconds(body.CurrentUtcOffset.Seconds);
        if (offset.Ticks % TimeSpan.TicksPerMinute != 0 || offset.Duration() > TimeSpan.FromHours(14))
            throw new TimeSourceException($"{Name}: invalid offset {body.CurrentUtcOffset.Seconds}s.");

        return new DateTimeOffset(local, offset);
    }

    private sealed record Payload(
        [property: JsonPropertyName("timeZone")] string? TimeZone,
        [property: JsonPropertyName("currentLocalTime")] string? CurrentLocalTime,
        [property: JsonPropertyName("currentUtcOffset")] OffsetPayload? CurrentUtcOffset);

    private sealed record OffsetPayload([property: JsonPropertyName("seconds")] int Seconds);
}
