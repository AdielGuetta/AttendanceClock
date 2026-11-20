namespace AttendanceClock.Api.Time;

/// <summary>Settings for the external time providers (appsettings.json, section "TimeProviders").</summary>
public sealed class TimeProviderOptions
{
    public const string SectionName = "TimeProviders";

    /// <summary>How long to wait for ONE provider before moving on to the next (PRD D-5: ~2.5 s each).</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(2.5);

    /// <summary>Primary provider (PRD D-4).</summary>
    public string TimeApiIoUrl { get; set; } = "https://timeapi.io/api/timezone/zone?timeZone=Europe%2FZurich";

    /// <summary>Backup provider (PRD D-4).</summary>
    public string TimeNowUrl { get; set; } = "https://time.now/developer/api/timezone/Europe/Zurich";
}
