using System.Net;
using AttendanceClock.Api.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AttendanceClock.Tests.Time;

/// <summary>Plan 05 test table: how the trusted clock behaves when providers answer well, badly, or not at all.</summary>
public class FallbackTrustedClockTests
{
    private static readonly TimeSpan Winter = TimeSpan.FromHours(1);
    private static readonly TimeSpan Summer = TimeSpan.FromHours(2);

    // A valid backup answer, used when a test needs the backup to succeed.
    private static FakeHttpHandler GoodBackup() =>
        FakeHttpHandler.Json(ProviderJson.TimeNow("2026-11-20T09:00:05.5+01:00", "2026-11-20T08:00:05.5Z"));

    private static ITrustedClock CreateClock(FakeHttpHandler primary, FakeHttpHandler backup)
    {
        var options = Options.Create(new TimeProviderOptions
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(300), // short, so the timeout test is quick
            TimeApiIoUrl = "https://primary.test/zurich",
            TimeNowUrl = "https://backup.test/zurich",
        });
        IExternalTimeSource[] sources =
        [
            new TimeApiIoSource(new HttpClient(primary), options),
            new TimeNowSource(new HttpClient(backup), options),
        ];
        return new FallbackTrustedClock(sources, options, NullLogger<FallbackTrustedClock>.Instance);
    }

    [Fact] // 1
    public async Task Primary_answers_in_winter_time()
    {
        var primary = FakeHttpHandler.Json(ProviderJson.TimeApiIo("2026-11-20T09:00:00.123", 3600));
        var backup = GoodBackup();

        var now = await CreateClock(primary, backup).GetZurichNowAsync();

        Assert.Equal("timeapi.io", now.Source);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 9, 0, 0, 123, Winter), now.Value);
        Assert.Equal(0, backup.Calls); // backup not needed
    }

    [Fact] // 2
    public async Task Primary_answers_in_summer_time()
    {
        var primary = FakeHttpHandler.Json(ProviderJson.TimeApiIo("2026-07-15T14:30:00", 7200));

        var now = await CreateClock(primary, GoodBackup()).GetZurichNowAsync();

        Assert.Equal(Summer, now.Value.Offset);
        Assert.Equal(new DateTime(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc), now.Value.UtcDateTime);
    }

    [Fact] // 3 (EC-7)
    public async Task Clocks_go_back_night_the_two_0230s_are_different_instants()
    {
        // 25 Oct 2026, 03:00 CEST → 02:00 CET, so 02:30 happens twice. The offset tells them apart.
        var first = await CreateClock(
            FakeHttpHandler.Json(ProviderJson.TimeApiIo("2026-10-25T02:30:00", 7200)), GoodBackup()).GetZurichNowAsync();
        var second = await CreateClock(
            FakeHttpHandler.Json(ProviderJson.TimeApiIo("2026-10-25T02:30:00", 3600)), GoodBackup()).GetZurichNowAsync();

        Assert.Equal(first.Value.DateTime, second.Value.DateTime); // same wall-clock time…
        Assert.Equal(TimeSpan.FromHours(1), second.Value - first.Value); // …but exactly one hour apart
    }

    [Fact] // 4
    public async Task Primary_times_out_so_backup_is_used()
    {
        var now = await CreateClock(FakeHttpHandler.Hangs(), GoodBackup()).GetZurichNowAsync();

        Assert.Equal("time.now", now.Source);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 9, 0, 5, 500, Winter), now.Value);
    }

    [Fact] // 5
    public async Task Primary_returns_http_500_so_backup_is_used()
    {
        var now = await CreateClock(FakeHttpHandler.Status(HttpStatusCode.InternalServerError), GoodBackup())
            .GetZurichNowAsync();

        Assert.Equal("time.now", now.Source);
    }

    [Fact] // 6
    public async Task Primary_returns_garbage_so_backup_is_used()
    {
        var now = await CreateClock(FakeHttpHandler.Json("<html>Service temporarily down</html>"), GoodBackup())
            .GetZurichNowAsync();

        Assert.Equal("time.now", now.Source);
    }

    [Fact] // 7
    public async Task Primary_reports_wrong_time_zone_so_it_is_rejected()
    {
        var primary = FakeHttpHandler.Json(ProviderJson.TimeApiIo("2026-11-20T09:00:00", 3600, zone: "Europe/London"));

        var now = await CreateClock(primary, GoodBackup()).GetZurichNowAsync();

        Assert.Equal("time.now", now.Source);
    }

    [Fact] // 8
    public async Task Primary_offset_breaks_zurich_rules_so_it_is_rejected()
    {
        // "+02:00" (summer time) in November is impossible for Zurich.
        var primary = FakeHttpHandler.Json(ProviderJson.TimeApiIo("2026-11-20T09:00:00", 7200));

        var now = await CreateClock(primary, GoodBackup()).GetZurichNowAsync();

        Assert.Equal("time.now", now.Source);
    }

    [Fact] // 9
    public async Task Backup_with_inconsistent_local_and_utc_times_is_rejected()
    {
        // Local says 09:00 (= 08:00 UTC) but UTC says 07:00. The answer contradicts itself.
        var badBackup = FakeHttpHandler.Json(ProviderJson.TimeNow("2026-11-20T09:00:00+01:00", "2026-11-20T07:00:00Z"));

        var error = await Assert.ThrowsAsync<TimeSourceUnavailableException>(() =>
            CreateClock(FakeHttpHandler.Status(HttpStatusCode.BadGateway), badBackup).GetZurichNowAsync());

        Assert.Contains(error.Failures, f => f.Contains("disagree"));
    }

    [Fact] // 10 (D-5: fail closed)
    public async Task Both_fail_so_the_clock_refuses_instead_of_guessing()
    {
        var error = await Assert.ThrowsAsync<TimeSourceUnavailableException>(() =>
            CreateClock(FakeHttpHandler.Hangs(), FakeHttpHandler.Status(HttpStatusCode.ServiceUnavailable))
                .GetZurichNowAsync());

        Assert.Equal(2, error.Failures.Count);
        Assert.Contains(error.Failures, f => f.StartsWith("timeapi.io") && f.Contains("timed out"));
        Assert.Contains(error.Failures, f => f.StartsWith("time.now") && f.Contains("503"));
    }
}
