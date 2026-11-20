# Plan 05: The trusted clock

**Status:** done ✅ · **Branch:** `feature/trusted-clock` · **PRD refs:** §4, D-2, D-3, D-4, D-5, D-6, EC-7, EC-8

## Goal
One component that answers **"what time is it in Zurich right now?"** using only the external time APIs chosen in
Plan 04. Every future punch asks this component and nothing else.

## Design

```
caller (later: ShiftService)
   │  GetZurichNowAsync()
   ▼
ITrustedClock  ──►  FallbackTrustedClock
                      1. try TimeApiIoSource   (primary, 2.5 s timeout)
                      2. on any failure → TimeNowSource (backup, 2.5 s timeout)
                      3. both failed → throw TimeSourceUnavailableException  (fail closed, D-5)
                      returns TrustedTime { Value: DateTimeOffset (Zurich offset), Source: "timeapi.io" | "time.now" }
```

| Piece | Responsibility |
|---|---|
| `ITrustedClock` | The only thing business code depends on (D-3). Tests swap in a fake. |
| `IExternalTimeSource` | One per provider: call the API, parse it, validate it, return a `DateTimeOffset`. *(Not named `ITimeProvider`, to avoid confusion with .NET's built-in `TimeProvider`, which reads the local clock.)* |
| `TimeApiIoSource` | Reads `currentLocalTime` + `currentUtcOffset` → `DateTimeOffset`. |
| `TimeNowSource` | Reads `datetime` (already has the offset), cross-checks it against `utc_datetime`. |
| `FallbackTrustedClock` | Tries the sources in order, with a per-attempt timeout; logs the provider, latency and outcome. |
| `TimeSourceUnavailableException` | Thrown when every source failed. The API turns it into `503` + error code `TIME_SOURCE_UNAVAILABLE`. |

Provider URLs and the timeout live in `appsettings.json` (`TimeProviders` section), not hard-coded.

## Validation: don't trust the API blindly (PRD §4.3)
An answer is **rejected** (and the next source is tried) if:
1. the HTTP call fails, times out, or returns non-200;
2. the JSON is missing fields or can't be parsed;
3. the reported time zone isn't `Europe/Zurich`;
4. the offset isn't +01:00 or +02:00;
5. **the offset doesn't match Zurich's DST rules for that instant.** We check it against the official time-zone rules
   (IANA tz database, bundled with .NET). That's the *rules*, not a clock, so the hard rule isn't broken;
6. (time.now only) the UTC time and the local time disagree.

## New endpoint (for seeing it work)
`GET /api/time/now` → `{ "zurichTime": "2026-11-20T09:15:02.123+01:00", "source": "timeapi.io" }`.
The UI will later use it to show the live clock (PRD §4.5). Punches will not call this endpoint; they use `ITrustedClock` directly.

## Tests (new project `AttendanceClock.Tests`, xUnit)
Real API calls are replaced by a **fake HTTP handler**, so tests are fast, repeatable, and can simulate outages:

| # | Scenario | Expected |
|---|---|---|
| 1 | Primary answers correctly (winter) | Time from timeapi.io, offset +01:00 |
| 2 | Primary answers correctly (summer) | Offset +02:00 |
| 3 | DST "fall back" hour (02:30 happens twice) | Each answer keeps its correct offset → correct, distinct instants (EC-7) |
| 4 | Primary times out | Backup used |
| 5 | Primary returns HTTP 500 | Backup used |
| 6 | Primary returns garbage JSON | Backup used |
| 7 | Primary reports the wrong time zone | Rejected → backup used |
| 8 | Primary's offset doesn't match Zurich rules | Rejected → backup used |
| 9 | Backup's UTC and local times disagree | Rejected |
| 10 | Both fail | `TimeSourceUnavailableException`; nothing falls back to the local clock |

Tests run inside Docker: `docker compose run --rm --no-deps api sh -c "cd /src && dotnet test"`.
`docker-compose.yml` gets bin/obj volumes for the test project, the same as the API's.

## Out of scope (later steps)
- Health check that pings the providers (`/health` showing "Degraded" when only the backup works). Noted for later.
- Retrying the same provider. Not worth it within the ~5 s budget; the backup *is* the retry.
- Using the clock for punches (that's the Clock In/Out step).

## Results (2026-11-20)
- Implemented as designed, in `backend/AttendanceClock.Api/Time/` (+ `Controllers/TimeController.cs`).
- **10/10 tests pass** (`AttendanceClock.Tests/Time/FallbackTrustedClockTests.cs`), all passing on the first run.
- Live check: `GET /api/time/now` → `{"zurichTime":"2026-11-20T07:26:37.1789553+01:00","source":"timeapi.io"}`.
  The log shows `Time from timeapi.io … in 326 ms` (first call), then 85 ms.
- Extra hardening added while coding: times *with* an expected offset are parsed strictly. A string without an offset is
  rejected instead of being silently read as the server's local zone, which would have been a hidden use of the local clock.
