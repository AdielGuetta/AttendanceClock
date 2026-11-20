using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AttendanceClock.Api.Time;

/// <summary>
/// Backup provider: time.now (PRD D-4).
/// Example answer: { "timezone": "Europe/Zurich", "datetime": "2026-11-19T20:15:40.978033+01:00",
///                   "utc_datetime": "2026-11-19T19:15:40.978033Z", ... }
/// It gives the same moment twice (local and UTC), so we cross-check them.
/// </summary>
public sealed class TimeNowSource(HttpClient http, IOptions<TimeProviderOptions> options) : IExternalTimeSource
{
    public string Name => "time.now";

    public async Task<DateTimeOffset> GetZurichTimeAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(options.Value.TimeNowUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new TimeSourceException($"{Name}: HTTP {(int)response.StatusCode}.");

        Payload? body;
        try { body = await response.Content.ReadFromJsonAsync<Payload>(cancellationToken); }
        catch (System.Text.Json.JsonException ex) { throw new TimeSourceException($"{Name}: invalid JSON.", ex); }

        if (body?.Timezone is null || body.Datetime is null || body.UtcDatetime is null)
            throw new TimeSourceException($"{Name}: response is missing required fields.");
        if (body.Timezone != ZurichTime.TimeZoneId)
            throw new TimeSourceException($"{Name}: returned time zone '{body.Timezone}', expected {ZurichTime.TimeZoneId}.");

        var local = ZurichTime.ParseWithOffset(body.Datetime, Name);
        var utc = ZurichTime.ParseWithOffset(body.UtcDatetime, Name);

        // Both fields describe the same instant. If they disagree, the answer can't be trusted.
        if ((local - utc).Duration() > TimeSpan.FromSeconds(1))
            throw new TimeSourceException($"{Name}: 'datetime' ({body.Datetime}) and 'utc_datetime' ({body.UtcDatetime}) disagree.");

        return local;
    }

    private sealed record Payload(
        [property: JsonPropertyName("timezone")] string? Timezone,
        [property: JsonPropertyName("datetime")] string? Datetime,
        [property: JsonPropertyName("utc_datetime")] string? UtcDatetime);
}
