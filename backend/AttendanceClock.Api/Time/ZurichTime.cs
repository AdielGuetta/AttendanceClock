using System.Globalization;

namespace AttendanceClock.Api.Time;

/// <summary>Zurich time-zone rules and strict parsing helpers shared by the providers.</summary>
public static class ZurichTime
{
    public const string TimeZoneId = "Europe/Zurich";

    /// <summary>
    /// The official IANA rules for Zurich (bundled with .NET). These are the summer/winter-time RULES,
    /// not a clock, so using them doesn't break the "external time only" rule.
    /// </summary>
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    private static readonly TimeSpan Winter = TimeSpan.FromHours(1); // CET
    private static readonly TimeSpan Summer = TimeSpan.FromHours(2); // CEST

    /// <summary>
    /// Rejects a provider's answer whose offset is impossible for Zurich, or doesn't match Zurich's
    /// DST rules for that instant (e.g. "+02:00" in November).
    /// </summary>
    public static void Validate(DateTimeOffset value, string sourceName)
    {
        if (value.Offset != Winter && value.Offset != Summer)
            throw new TimeSourceException($"{sourceName}: offset {value.Offset} is not a Zurich offset (+01:00/+02:00).");

        var expected = Zone.GetUtcOffset(value.UtcDateTime);
        if (value.Offset != expected)
            throw new TimeSourceException(
                $"{sourceName}: offset {value.Offset} doesn't match Zurich rules for {value.UtcDateTime:u} (expected {expected}).");
    }

    /// <summary>Parses a local date-time with NO offset, e.g. "2026-11-19T20:15:40.468801".</summary>
    public static DateTime ParseLocal(string text, string sourceName)
    {
        string[] formats = ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd'T'HH:mm:ss"];
        if (!DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            throw new TimeSourceException($"{sourceName}: can't parse local time '{text}'.");
        return result; // Kind = Unspecified: the offset is attached by the caller
    }

    /// <summary>
    /// Parses a date-time that MUST include an offset or "Z", e.g. "2026-11-19T20:15:40.97+01:00".
    /// Strings without an offset are rejected. Otherwise .NET would silently assume the server's local zone.
    /// </summary>
    public static DateTimeOffset ParseWithOffset(string text, string sourceName)
    {
        string[] formats =
        [
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", "yyyy-MM-dd'T'HH:mm:sszzz",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'",
        ];
        if (!DateTimeOffset.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
            throw new TimeSourceException($"{sourceName}: can't parse time with offset '{text}'.");
        return result;
    }
}
