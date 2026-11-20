# Automated tests and edge cases (מקרי קצה)

The assignment asks us to **identify edge cases and complex scenarios and handle them**. This document maps each edge case
(IDs from [PRD §6](PRD.md)) to **how it's handled** and **which automated test proves it**.

**Current result: 58 / 58 backend test cases + 13 / 13 frontend tests passing.**

```bash
docker compose run --rm --no-deps api sh -c "cd /src && dotnet test"              # backend
docker compose run --rm --no-deps web sh -c "npm install && npx vitest run"       # frontend
```

---

## The three kinds of tests

| Kind | Where | How it works | Why |
|---|---|---|---|
| **Trusted clock unit tests** (10) | `Tests/Time/FallbackTrustedClockTests.cs` | A **fake HTTP handler** replaces the internet and returns any answer we want: correct, broken, slow, lying | Simulate outages and DST nights without waiting for them; no real network, so fast and repeatable |
| **Login integration tests** (9) | `Tests/Auth/AuthTests.cs` | The **real app** runs in memory (`ApiFactory`) against the real SQL Server, in a **separate database** (`AttendanceClock_Tests`) reset every run | Tests the real security behaviour end to end; development data is never touched |
| **Punch integration tests** (12) | `Tests/Punches/PunchTests.cs` | Real app + test DB, with the time APIs swapped for a **fake clock** we control ("it's now 22:00", "the service is down") | Lets us "travel" to a DST night or a 15-hour shift; the fake exists **only in tests** |
| **History integration tests** (10 methods, 12 cases) | `Tests/History/HistoryTests.cs` | Real app + test DB + fake clock; shifts on many days inserted directly | Day boundaries, DST days, totals, privacy |
| **Manager integration tests** (13 methods, 15 cases) | `Tests/Admin/AdminTests.cs` | Real app + test DB + fake clock, logged in as a manager | Permissions, corrections, audit trail, version stamps, DST input |

The login, punch, history and manager test classes share the test database, so they never run in parallel (`DatabaseCollection`).

---

## Edge cases: how they're handled and proven

Legend: ✅ automated test · 🔒 enforced by the database · 🟡 handled by design, no dedicated test yet · ⏳ planned (later step) · ⚠️ known limitation

### Time (the core requirement)
| ID | Edge case | Handling | Proof |
|---|---|---|---|
| EC-8 | Time API **down** | Try the backup; if both fail, refuse with `503 TIME_SOURCE_UNAVAILABLE`, nothing saved | ✅ Time #10 · ✅ Punch #7 |
| EC-8 | Time API **slow** | 2.5 s per provider, then the backup | ✅ Time #4 |
| EC-8 | Time API returns **HTTP error / garbage** | Rejected, backup used | ✅ Time #5, #6 |
| — | Provider returns the **wrong time zone** | Rejected, backup used | ✅ Time #7 |
| — | Provider's **offset breaks Zurich's rules** (e.g. summer offset in November) | Rejected, checked against the official IANA rules | ✅ Time #8 |
| EC-9 | Provider **contradicts itself** (local ≠ UTC) | Rejected | ✅ Time #9 |
| EC-9 | Providers disagree, so **Clock Out ≤ Clock In** | Refused (`503 TIME_SOURCE_INCONSISTENT`), shift stays open | ✅ Punch #11 · 🔒 check constraint |
| — | Time string **without an offset** | Rejected (otherwise .NET would silently assume the server's zone) | 🟡 strict parser, no dedicated test |
| EC-6 | **DST start** (clocks jump forward) during a night shift | Durations computed on real instants → 7 h, not 8 | ✅ Punch #6 · ✅ Time #2 (summer offset) |
| EC-7 | **DST end**: 02:30 happens twice | The stored offset tells the two apart | ✅ Time #3 |
| EC-10 | Employee's **browser clock / time zone is wrong** | Irrelevant: the client never sends a time; the server takes it from the API | ✅ Punch #1 (time = trusted clock's) |
| EC-17 | **Shift-start rush** (hundreds of punches per minute) | One external call per punch may hit the free API's limits | ⚠️ documented; mitigation not built |

### Clock In / Clock Out
| ID | Edge case | Handling | Proof |
|---|---|---|---|
| EC-1 | **Double-click** on Clock In | DB unique index → `409 ALREADY_ON_SHIFT` | ✅ Punch #8 (10 simultaneous → exactly 1 shift) · 🔒 |
| EC-2 | **Two tabs / two devices** at the same time | Same mechanism | ✅ Punch #8 · 🔒 |
| EC-3 | **Clock Out with no open shift** (stale tab) | `409 NOT_ON_SHIFT` | ✅ Punch #3 |
| EC-4 | **Forgot to clock out**, clocks in the next day | `409` with "on shift since …" | ✅ Punch #2 |
| EC-5 | **Overnight shift** (22:00 → 06:00) | Duration from instants; counted on the start date | ✅ Punch #5 |
| F7 | **Very long shift** (> 14 h) | Recorded honestly, flagged `TooLong` | ✅ Punch #10 |
| EC-11 | **Very short accidental shift** (seconds) | Recorded honestly (0 min) | 🟡 seen in the live run, no dedicated test |
| EC-15 | Time fetched, then the **DB write fails** | One `SaveChanges` call = one transaction, so nothing is half-saved; the user retries | 🟡 by design |
| — | **Punching without logging in** | `401` | ✅ Punch #9 |

### History and totals
| ID | Edge case | Handling | Proof |
|---|---|---|---|
| D-10 / EC-5 | **Overnight shift** in history | Counted once, on its start date | ✅ History #2 |
| EC-6 | Shift starting **00:30 on a DST day** (a 23-hour day) | Day boundaries computed from Zurich rules → right day | ✅ History #3 |
| — | Shift starting **just before midnight** of the range | Not included (belongs to the previous day) | ✅ History #4 |
| — | **Seconds that don't fill a minute** | Each shift rounds to the nearest minute; total = sum of rows (always adds up, no systematic loss) | ✅ History #1b |
| — | **Open shift** | Shown separately, not counted in totals | ✅ History #6 |
| — | **Someone else's shifts** | Never returned: employee id from the token, not the URL | ✅ History #5 |
| — | **Huge or reversed date range** | `400 INVALID_RANGE` (max 93 days) | ✅ History #8 |

### Login and security
| ID | Edge case | Handling | Proof |
|---|---|---|---|
| — | Wrong password | `401 INVALID_CREDENTIALS` | ✅ Auth #2 |
| — | **Unknown username** (user enumeration) | The *same* message and timing as a wrong password | ✅ Auth #3 |
| — | **Deactivated** employee | The same generic `401` | ✅ Auth #4 |
| EC-14 | **Missing / tampered** token | `401` | ✅ Auth #5, #7 |
| EC-14 | **Expired** token | `401` (standard JWT validation, 30 s clock skew) | 🟡 not tested (would need waiting or clock control) |
| — | **Password guessing** (brute force) | 5 attempts / min / IP → `429 TOO_MANY_ATTEMPTS` | ✅ Auth #8 |
| — | Passwords readable from the DB | Stored only as salted PBKDF2 hashes | ✅ Auth #9 |

### Manager corrections
| ID | Edge case | Handling | Proof |
|---|---|---|---|
| — | **Employee calls a manager endpoint** | `403` (server-side role check) | ✅ Admin #1 |
| EC-4 / F7 | **Forgotten open shift** | Flagged "ForgotClockOut" after 14 h (computed against the trusted clock); manager closes it → employee can clock in again | ✅ Admin #2, #5 |
| F6 | **A correction must be explained and visible** | Reason ≥ 5 chars; source `ManualCorrection`; flag `Corrected`; one audit row per changed field, stamped by the trusted clock | ✅ Admin #4, #6, #12 |
| EC-13 | Correction puts **Clock Out before Clock In** | `400` | ✅ Admin #7 |
| EC-13 | Correction **in the future** | `400 TIME_IN_FUTURE` (trusted "now") | ✅ Admin #8 |
| EC-13 | Correction **overlaps another shift** | `409 OVERLAPS_ANOTHER_SHIFT` | ✅ Admin #9 |
| EC-2 (edit) | **Shift changed while the manager was editing** | Version stamp → `409 SHIFT_MODIFIED` | ✅ Admin #10 · 🔒 rowversion |
| EC-7 | Manager types **02:30 on the fall-back night** (happens twice) | `400 AMBIGUOUS_TIME`; with an explicit offset it's accepted, and the duration is correct | ✅ Admin #11 |
| EC-6 | Manager types **02:30 on the spring-forward night** (doesn't exist) | `400 INVALID_LOCAL_TIME` | ✅ Admin #11b |
| EC-16 | Manager corrects **their own** shift | Allowed, audited and flagged like any other | ⚠️ no four-eyes rule |
| EC-12 | Employee **deactivated while on shift** | No employee-management screen yet | ⏳ not built |

---

## All tests

### Trusted clock: `FallbackTrustedClockTests` (10)
| # | Test | Proves |
|---|---|---|
| 1 | Primary answers in winter time | Correct value, +01:00, backup not called |
| 2 | Primary answers in summer time | +02:00 handled |
| 3 | Clocks-go-back night: the two 02:30s are different instants | EC-7 |
| 4 | Primary times out → backup used | 2.5 s budget works |
| 5 | Primary returns HTTP 500 → backup used | |
| 6 | Primary returns garbage → backup used | |
| 7 | Primary reports the wrong time zone → rejected | |
| 8 | Primary offset breaks Zurich rules → rejected | Validation against IANA rules |
| 9 | Backup's local and UTC times disagree → rejected | |
| 10 | Both fail → refuses instead of guessing | Fail closed (D-5) |

### Login: `AuthTests` (9)
| # | Test | Proves |
|---|---|---|
| 1 | Correct password → token + role | |
| 2 | Wrong password → `INVALID_CREDENTIALS` | |
| 3 | Unknown user gets exactly the same answer | No user enumeration |
| 4 | Deactivated employee can't log in, even with the right password | |
| 5 | `/me` without a token → 401 | |
| 6 | `/me` with a valid token → the right employee | |
| 7 | `/me` with a tampered token → 401 | Signature check |
| 8 | 6th login attempt within a minute → 429 | Brute-force limit |
| 9 | Passwords stored as hashes | |

### Clock In / Clock Out: `PunchTests` (12)
| # | Test | Proves |
|---|---|---|
| 1 | Clock In records the time from the trusted clock | Time + source + Zurich offset |
| 2 | Clocking in twice is refused and says since when | EC-4 |
| 3 | Clocking out while off shift is refused | EC-3 |
| 4 | 08:00 → 16:30 = 510 min, status back to OffShift | |
| 5 | Overnight 22:00 → 06:00 = 480 min | EC-5 |
| 6 | Clocks jump forward night = 7 h, not 8 | EC-6 |
| 7 | Time service down → nothing saved | D-5, EC-8 |
| 8 | 10 simultaneous Clock Ins → exactly 1 shift | EC-1/2, D-9 |
| 9 | Punching without logging in → 401 | |
| 10 | 15-hour shift → recorded and flagged `TooLong` | F7 |
| 11 | Clock Out time before Clock In → rejected, shift stays open | EC-9 |
| 12 | Status while on shift shows since when | F3 |

### History: `HistoryTests` (10 methods, 12 cases)
| # | Test | Proves |
|---|---|---|
| 1 | Daily and range totals add up | 480 + 510 = 990 min |
| 1b | Each shift rounds to the nearest minute and totals add up | 1:40 → 2, 1:20 → 1, 0:30 → 1; total 4 = sum of rows |
| 2 | Overnight shift counts only on its start date | D-10 |
| 3 | Shift at 00:30 on the DST day lands on that day | EC-6, Zurich day boundaries |
| 4 | Shifts outside the range are not included | Boundary just before midnight |
| 5 | Another employee's shifts are never included | Privacy |
| 6 | Open shift returned separately, not counted | |
| 7 | No dates → this week, from the trusted clock | Mon–Sun in Zurich |
| 8 | Invalid range refused (3 cases: reversed, > 93 days, one date only) | `400 INVALID_RANGE` |
| 9 | History requires login | 401 |

---

### Manager: `AdminTests` (13 methods, 15 cases)
| # | Test | Proves |
|---|---|---|
| 1 | Employees can't use manager endpoints | 403 |
| 2 | On-shift list shows open shifts and flags forgotten ones | F5, F7 |
| 3 | Shift list filters by employee and flagged-only | F5 |
| 4 | Correcting a Clock Out → labelled, flagged, audited (old/new/reason/manager/trusted time) | F6 |
| 5 | Closing a forgotten shift lets the employee clock in again; audit shows "(open)" | EC-4 |
| 6 | Reason required (3 cases: missing, empty, too short) | F6 |
| 7 | Clock Out must stay after Clock In | |
| 8 | Times in the future are refused | EC-13 |
| 9 | A correction can't overlap another shift | EC-13 |
| 10 | A stale version stamp is refused | Optimistic concurrency |
| 11 | Ambiguous fall-back 02:30 needs an offset; with +01:00 → 330 min | EC-7 |
| 11b | Non-existent spring-forward 02:30 is refused | EC-6 |
| 12 | Audit history lists every change in order | D-11 |

### Frontend: `frontend/src/time.test.ts` (Vitest, 13)
| # | Test | Proves |
|---|---|---|
| 1 | Server time "…08:02:11+01:00" → "08:02:11 CET" | Display reads Zurich time straight from the server |
| 2 | "+02:00" → CEST | Summer label |
| 3 | 23:30 Zurich stays 23:30 / same date | Independent of the device's time zone (EC-10) |
| 4 | Live clock: UTC instant → Zurich winter time + date | |
| 5 | Live clock: UTC instant → Zurich summer time | |
| 6 | Label switches CET → CEST exactly at 01:00 UTC on 29 Mar 2026 (01:59:59 → 03:00:00) | DST on screen |
| 7 | 510 → "8:30", 5 → "0:05" | Duration format |
| 8 | 192 → "3 h 12 min" | "On shift for …" |
| 9 | Date steps across month end and the DST day | Week navigation |
| 10 | Seven dates of a week | |
| 11 | Forgotten-shift alert quiet at exactly 14 h | Plan 11 |
| 12 | Alert fires just after 14 h | Plan 11 |
| 13 | Open time measured on real instants, across a DST change | Plan 11 |

## Manual checks done along the way
- **Database rules (plan 03):** 5 direct SQL tests inside a rolled-back transaction: first shift accepted, second open shift
  rejected, Clock Out before Clock In rejected, normal Clock Out accepted, next-day shift accepted.
- **Live runs** against the real time API: `/api/time/now` (plan 05), real logins (plan 06), and a real clock-in/out cycle (plan 07).
- **Browser walkthrough of the UI (plan 09):** wrong password, login, Clock In, refresh, a **stale tab after a second
  device clocked out** (409 → message + self-refresh), My week, phone-size layout, log out.
- **Browser walkthrough of the manager screens (plan 10):** forgotten shift flagged → closed with a reason → history entry →
  future time refused → the employee sees "Corrected by manager" and has no Team tab.
