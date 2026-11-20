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
| 11 | **JWT login, 60-minute tokens** (D-8) | Stateless and simple with a separate React app. Trade-off: a token can't be revoked early; the short lifetime limits the risk. |
| 12 | **Passwords hashed with Identity's `PasswordHasher` only** | Microsoft's tested PBKDF2 + salt, without the full Identity table set we don't need. |
| 13 | **One generic login error, equal timing, 5 attempts/min per IP** | Doesn't reveal which usernames exist; stops password guessing. |
| 14 | **No self-registration; demo accounts seeded in Development** (D-1) | Internal tool. Reviewers can log in immediately (accounts listed in the README). |
| 15 | **The server never trusts the UI** | The UI shows only the valid button, but stale tabs, double-clicks and direct API calls still reach the server. Rules are enforced in `ShiftService` and, finally, by the database. |
| 16 | **No `Idempotency-Key` for punches** (revised PRD F2) | The DB unique index already guarantees one open shift; a repeated punch gets a clear `409` with the current state. Keys would add a table for a nicer response only. |
| 17 | **`/status` doesn't call the time API** | The UI checks status often; external calls there would worsen the rush risk (EC-17). Elapsed time on screen is cosmetic; recorded times always come from the API. |
| 18 | **Clock In must be after the previous Clock Out; Clock Out after Clock In** | Two providers that disagree by a second can't create overlapping or negative shifts. The punch is refused with `503 TIME_SOURCE_INCONSISTENT` instead. |
| 19 | **Shifts over 14 h are recorded and flagged `TooLong`**, not blocked | The record stays honest; the manager reviews it (F7). |
| 20 | **History day boundaries use Zurich's official rules**; a shift belongs to its Clock In date (D-10) | DST days are 23 or 25 hours long. An overnight shift is counted once, on its start date. |
| 21 | **Each shift rounds to the nearest minute; totals = the sum of the rows** | First version rounded each shift *down* and added those, which loses time systematically (found in the live check). Adding exact durations fixed the loss, but then the rows didn't visibly add up to the total. Nearest-minute rounding is fair in both directions (≤ 30 s per shift) **and** what the employee sees always adds up. |
| 22 | **History: only your own shifts (id from the token), max 93 days, default = current Zurich week via the trusted clock** | No peeking at others' data via the URL; bounded queries; "today" comes from a trusted source. |
| 23 | **React UI runs in Docker too (Node 24, Vite), with a Vite proxy for `/api`** | Vite's unsigned native helper would face the same Smart App Control block; the proxy means one address for the browser and no CORS setup. |
| 24 | **One big context-aware button, disabled while recording** | The UI prevents ordinary double-clicks; the server still enforces the rules (#15). After any punch or error, the status is re-read from the server. |
| 25 | **Times shown straight from the server's ISO string (already Zurich + offset), labelled CET/CEST** | No conversion on the device, so a phone set to another zone still shows Zurich time. The live clock is cosmetic (server time + elapsed `performance.now()`). |
| 26 | **No router or UI library: three screens, a tab switch and plain CSS** | Less to install, explain and maintain for a small app. |
| 27 | **Login badge kept in `localStorage`** | Survives a refresh. Trade-off: readable by scripts if the page had an XSS hole. Mitigated: React escapes all output, no raw HTML, 60-minute expiry. |
| 28 | **App on port 5174, not Vite's default 5173** | During testing, another local Vite project was on 5173 and the browser showed the *wrong app*. Moved ours rather than stopping the user's other project. |
| 29 | **Manager corrections: reason required, labelled `ManualCorrection`, shift flagged `Corrected`** (F6) | The only non-API times in the system, so each one is explained and visible: the "external time" promise stays honest. |
| 30 | **Append-only `ShiftAudits` table**: who, when (trusted clock), field, old → new, reason (D-11) | A permanent, attributable history. Rows are never updated or deleted; no cascade delete. |
| 31 | **Version stamp on edits** (`rowversion`, sent back by the UI) | If the employee clocked out while the manager was editing, the save is refused (`409 SHIFT_MODIFIED`) instead of silently overwriting. |
| 32 | **Managers type Zurich wall time; DST is resolved by the server** | 02:30 on the fall-back night is ambiguous → the UI asks "the first or the second 02:30?"; 02:30 on the spring-forward night doesn't exist → refused. |
| 33 | **"Forgot to clock out" is computed on read** (open > 14 h, against the trusted clock), not stored | It changes with time; storing it would go stale. If the time service is down, lists still load, just without that flag. |
| 34 | **Manager-only endpoints enforced on the server** (`[Authorize(Roles = "Manager")]` → 403); the Team tab is just a convenience | The UI never decides permissions. |

## Edge cases

| Case | Handling | Status |
|---|---|---|
| Time API down / slow / garbage (EC-8) | Backup provider → otherwise 503 `TIME_SOURCE_UNAVAILABLE` | ✅ built + tested |
| Provider returns an impossible offset or wrong zone | Rejected, next provider tried | ✅ built + tested |
| DST "fall back": 02:30 happens twice (EC-7) | The offset distinguishes the two instants | ✅ tested |
| Two open shifts (double-click, two tabs) (EC-1/2) | Database unique filtered index → `409 ALREADY_ON_SHIFT` | ✅ tested: 10 simultaneous Clock Ins → exactly 1 shift |
| Clock Out with no open shift (stale tab) (EC-3) | `409 NOT_ON_SHIFT` | ✅ tested |
| Forgot to clock out, tries to clock in next day (EC-4) | `409` with "on shift since …" | ✅ tested |
| Overnight shift (EC-5) | Duration from the instants; counted on the start date | ✅ tested (480 min) |
| DST spring-forward during a night shift (EC-6) | 22:00 → 06:00 = 7 h, not 8 | ✅ tested |
| Clock Out before Clock In (EC-9) | Rejected in code (`503 TIME_SOURCE_INCONSISTENT`) + DB check constraint | ✅ tested |
| Time API down during a punch | `503`, nothing saved | ✅ tested |
| Shift over 14 h (F7) | Saved, flagged `TooLong` | ✅ tested |
| History: overnight shift / shift at 00:30 on a DST day | Counted on its start date; boundaries from Zurich rules | ✅ tested |
| History: another employee's shifts | Never returned (id from the token) | ✅ tested |
| UI: stale tab / second device presses an outdated button | Server `409` → clear message, screen refreshes itself | ✅ checked live in the browser |
| UI: device clock or time zone wrong | Recorded and displayed times come from the server; the live clock is server-seeded | ✅ by design + formatting tests |
| UI: login badge expires mid-session | Any `401` → back to the login screen with a message | ✅ built (not yet tested automatically) |
| Expired / tampered / missing login token (EC-14) | 401; the UI will send the user back to login | ✅ API side built + tested |
| Deactivated employee tries to log in | Same generic 401 as a wrong password | ✅ tested |
| Forgotten open shift (EC-4) | Flagged in "On shift now"; manager closes it with a reason → employee can clock in again | ✅ tested + browser walkthrough |
| Correction overlaps another shift / is in the future / out before in (EC-13) | `409` / `400` with a clear code | ✅ tested |
| Shift changed while a manager edits it | `409 SHIFT_MODIFIED`, reload first | ✅ tested |
| Manager types 02:30 on a DST night | Fall back: "which one?" (offset required). Spring forward: "doesn't exist" | ✅ tested |
| Manager corrects their own shift (EC-16) | Allowed, audited and flagged like any correction | ⚠️ no four-eyes rule (known limitation) |
| Employee calls manager endpoints | `403` | ✅ tested |
| Deactivating an employee mid-shift (EC-12) | Not built (no employee management UI) | ⏳ not done |

## What I chose not to do
- **No fallback to the server clock**, even as a last resort (it would break the core rule silently).
- **No "anchor + stopwatch" time** (sync once, then count locally): resilient, but arguably local time.
- **No retry of the same provider**: within the ~5 s budget, the backup *is* the retry.
- **No keyed time APIs** (TimeZoneDB, API Ninjas…): reviewers would have to sign up.

## Known limitations and risks
- **The backup provider is flaky**: time.now failed ~20–30% of calls in our test. Both failing at once should be rare, but when it happens the punch is refused.
- **Free third-party APIs** have no uptime guarantee. A paid or self-hosted time source would be the production answer.
- **Shift-start rush (EC-17):** every punch makes one external time call, so hundreds of punches in the same minute could
  hit the free provider's rate limits. SQL Server isn't the bottleneck. Mitigation (not built): let punches arriving
  within the same second share one in-flight time request, or use a paid provider.
- `/health` doesn't check the time providers yet (planned).
- No password change/reset or refresh tokens yet; logging out = the client discards its token.
- The rate limit is per IP and in memory, so it resets on restart and isn't shared across multiple servers.
- The UI runs on the Vite **dev server**; a production setup would build static files and serve them (e.g. from the API or a CDN).
- No automated UI (end-to-end) tests yet: the screens were verified by a manual browser walkthrough, plus unit tests for the formatting.
- **No employee management screen**: employees are seeded; creating, deactivating or resetting passwords isn't built (EC-12 open).
- **No four-eyes rule**: a manager can correct their own shift (audited and flagged, but not approved by someone else).
- No CSV/payroll export; the shift list has no paging (one week at a time, max 93 days).

## Working with Claude Code
- Rules for the AI live in [CLAUDE.md](CLAUDE.md); each big step starts with a plan in [docs/plans/](docs/plans/).
- **Where it needed correcting:**
  - after switching to Docker, the PRD still said "no Docker". I caught it and had it fixed;
  - `dotnet add package` pulled EF Core 11 (incompatible with .NET 10), so versions are pinned;
  - sqlcmd needed `-I` to work with the filtered index;
  - a PowerShell 5.1 file edit garbled non-ASCII characters (wrong default encoding). Caught, fixed and added to CLAUDE.md;
  - its fix for the history rounding bug was mathematically right but made the rows not visibly add up to the total.
    I pushed back from the user's point of view, and we switched to nearest-minute rounding (DECISIONS #21);
  - in the UI walkthrough, a stale-tab message said "Refresh to see your status" although the screen refreshes itself. Reworded;
  - its merge procedure (`git switch main` before merging) briefly deleted the `frontend/` folder, so Vite restarted without
    its config and the app became unreachable. Diagnosed from the container log, fixed, and the procedure changed in CLAUDE.md.
