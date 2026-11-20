# DECISIONS

The main decisions, the edge cases, what was left out, and known limitations. Kept short. The full reasoning is in
[docs/PRD.md](docs/PRD.md) (IDs like `D-5`, `EC-7`) and the step plans in [docs/plans/](docs/plans/).

*This document grows with the project. Sections marked "(planned)" aren't built yet.*

---

## Key decisions

| # | Decision | Why |
|---|---|---|
| 1 | **Time is fetched by the server, never sent by the client** (D-2) | A browser clock can be changed by anyone; the client only sends intent ("clock me in"). |
| 2 | **One `ITrustedClock`; providers behind it** (D-3) | Business code has exactly one place to get "now". Tests swap in fakes to simulate outages and DST. |
| 3 | **Primary timeapi.io, backup time.now** (D-4) | Chosen by measurement: 6 candidates, 10 calls each ([plan 04](docs/plans/04-time-providers.md)). timeapi.io was 10/10; worldtimeapi.org was down. Both are keyless, so reviewers don't need to sign up. |
| 4 | **Fail closed: both providers down → punch rejected (503)** (D-5) | A wrong time is worse than a missing punch, which a manager can add later with a reason. Never falls back to the local clock. |
| 5 | **2.5 s timeout per provider** | timeapi.io sometimes takes ~1.3 s; a short per-attempt budget lets a slow primary fall through to the backup quickly. |
| 6 | **Validate every answer** (§4.3) | Zone must be Europe/Zurich; offset must be +01:00/+02:00 **and** match Zurich's DST rules for that instant; time.now's local and UTC fields must agree; times without an offset are rejected. |
| 7 | **Store `datetimeoffset` (instant + Zurich offset)** (D-6) | One column keeps the true instant (DST-safe durations) and the wall time the employee saw. It replaced a two-column design during implementation. |
| 8 | **Safety rules in the database, not only in code** (D-9) | Filtered unique index = at most one open shift per employee, even under concurrent requests; check constraint = Clock Out after Clock In. |
| 9 | **Docker Compose for local dev** (D-12) | Windows Smart App Control on the dev machine blocks locally built .NET assemblies, and turning it off would require a Windows reinstall. Docker needed no system changes, and reviewers get a one-command start. |
| 10 | **.NET 10 (LTS) + EF Core 10** | Long-term support. EF Core 11 needs .NET 11, so packages are pinned to 10.0.x. |

## Edge cases

| Case | Handling | Status |
|---|---|---|
| Time API down / slow / garbage (EC-8) | Backup provider → otherwise 503 `TIME_SOURCE_UNAVAILABLE` | ✅ built + tested |
| Provider returns an impossible offset or wrong zone | Rejected, next provider tried | ✅ built + tested |
| DST "fall back": 02:30 happens twice (EC-7) | The offset distinguishes the two instants | ✅ tested |
| Two open shifts (double-click, two tabs) (EC-1/2) | Database unique filtered index | ✅ enforced in DB (API handling planned) |
| Clock Out before Clock In (EC-9) | Database check constraint | ✅ enforced in DB |
| Forgot to clock out, overnight shifts, corrections | See PRD §6 | (planned) |

## What I chose not to do
- **No fallback to the server clock**, even as a last resort (it would break the core rule silently).
- **No "anchor + stopwatch" time** (sync once, then count locally): resilient, but arguably local time.
- **No retry of the same provider**: within the ~5 s budget, the backup *is* the retry.
- **No keyed time APIs** (TimeZoneDB, API Ninjas…): reviewers would have to sign up.

## Known limitations and risks
- **The backup provider is flaky**: time.now failed ~20–30% of calls in our test. Both failing at once should be rare, but when it happens the punch is refused.
- **Free third-party APIs** have no uptime guarantee. A paid or self-hosted time source would be the production answer.
- `/health` doesn't check the time providers yet (planned).

## Working with Claude Code
- Rules for the AI live in [CLAUDE.md](CLAUDE.md); each big step starts with a plan in [docs/plans/](docs/plans/).
- **Where it needed correcting:**
  - after switching to Docker, the PRD still said "no Docker". I caught it and had it fixed;
  - `dotnet add package` pulled EF Core 11 (incompatible with .NET 10), so versions are pinned;
  - sqlcmd needed `-I` to work with the filtered index.
