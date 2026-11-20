# Plan 07: Clock In / Clock Out

**Status:** done ✅ · **Branch:** `feature/clock-in-out` · **PRD refs:** F2, F3, F7, D-2, D-5, D-9, D-10, EC-1–5, EC-9, EC-15

## Goal
A logged-in employee (or manager) can **clock in**, **clock out**, and see **"am I on shift right now?"**.
Every recorded time comes from the trusted clock (Plan 05). The database rules (Plan 03) are the final guard.

## Endpoints (all require login)
| Method | Route | Does |
|---|---|---|
| POST | `/api/punches/clock-in` | Starts a shift. No body: the client never sends a time (D-2). |
| POST | `/api/punches/clock-out` | Ends the open shift. |
| GET | `/api/punches/status` | `{ state: "OnShift" \| "OffShift", openShift? }` |

Success returns the **recorded shift**, e.g. `{ id, clockIn: "2026-11-20T08:02:11+01:00", clockInSource: "timeapi.io",
clockOut: null, durationMinutes: null, flags: [] }`. The UI shows *this* time as the confirmation.

## Clock In: step by step
1. Who's calling? Read the employee id from the login token; the employee must still be active.
2. **Already on shift?** → `409 ALREADY_ON_SHIFT`, including *since when*, so the UI can say "You're on shift since Mon 08:02"
   (EC-4: forgot to clock out yesterday). Checked **before** calling the time API, to save a call.
3. Get the time from the **trusted clock**. Both providers down → `503 TIME_SOURCE_UNAVAILABLE`, nothing saved (D-5).
4. Sanity check: the new Clock In must be **after the previous shift's Clock Out** (no overlapping shifts if two providers disagree slightly).
5. Save. If a simultaneous request won the race, the database's unique index refuses this one → `409 ALREADY_ON_SHIFT` (D-9, EC-1/2).

## Clock Out: step by step
1. Find the open shift. None → `409 NOT_ON_SHIFT` (EC-3: stale tab).
2. Get the time from the trusted clock (503 if unavailable).
3. It must be **after** the Clock In, otherwise reject and keep the shift open (EC-9; the DB check constraint backs this up).
4. Longer than **14 h**? Flag it `TooLong` for the manager (F7). It's still recorded honestly.
5. Save with the `rowversion` check: if something changed the shift meanwhile (a double clock-out), → `409`.

## Design decisions in this step
| Decision | Why |
|---|---|
| **No `Idempotency-Key` header** (the PRD listed it as optional) | The database already guarantees correctness: a repeated or simultaneous punch gets a clear `409` that includes the current state, and the UI refreshes. Keys would add a table and code for a nicer response only. Recorded in DECISIONS.md. |
| **`/status` does NOT call the time API** | The UI checks status often. Calling the external API each time would worsen the rush problem (EC-17). Status returns the Clock In time; the UI shows "on shift for 3h 12m" using its live display clock (cosmetic, PRD §4.5). |
| **Totals (today / this week) move to the history step** | They belong with the history screen (F4), and also need a trusted "now". Keeps this step focused. |
| A shift belongs to the Zurich date of its Clock In (D-10) | Overnight shifts (22:00 → 06:00) count once, on the start date. |
| Thin controller + `ShiftService` holding the rules | Rules live in one testable place. |

## Tests (real app in memory + test DB; the time API is replaced by a controllable **fake clock**)
| # | Scenario | Expected |
|---|---|---|
| 1 | Clock in | 201; time and source = what the fake clock said |
| 2 | Clock in twice | 2nd → 409 `ALREADY_ON_SHIFT` with the open shift's start |
| 3 | Clock out while off shift | 409 `NOT_ON_SHIFT` |
| 4 | In 08:00 → out 16:30 | 510 minutes; status back to OffShift |
| 5 | Overnight: 22:00 → 06:00 | 480 minutes |
| 6 | **DST night**: 27→28 Mar 2027, 22:00 → 06:00 (clocks jump forward) | **420** minutes, not 480 |
| 7 | Time API down at Clock In | 503; **no shift saved** |
| 8 | **10 simultaneous Clock Ins** by the same employee | Exactly **1** succeeds, 9 get 409; one open shift in the DB |
| 9 | No login token | 401 |
| 10 | 15-hour shift | Recorded and flagged `TooLong` |
| 11 | Provider returns a Clock Out time before the Clock In | Rejected; the shift stays open |
| 12 | Status while on shift | OnShift + the Clock In time |

## Out of scope (later steps)
History and totals (F4), the manager views and corrections (F5–F7), the `ForgotClockOut` flag on open shifts (computed
in the manager view), and the React screens.

## Results (2026-11-20)
- Implemented in `backend/AttendanceClock.Api/Punches/` (`ShiftService` holds the rules, `PunchesController` is thin).
- **12/12 punch tests pass, 31/31 overall.** Test classes that share the test database now run one after another (`DatabaseCollection`).
- **The concurrency test proved the database guard is needed.** Out of 10 simultaneous Clock Ins, several got past
  the code's "already on shift?" check at the same moment. SQL Server's unique index rejected them (logged as
  `duplicate key … UX_Shifts_OneOpenShiftPerEmployee`), the service turned that into `409`, and exactly 1 shift was saved.
- Live run as `yossi` (real timeapi.io):
  - status → OffShift
  - clock-in → `201`, `"clockIn":"2026-11-20T08:36:09.916+01:00","clockInSource":"timeapi.io"`
  - status → OnShift
  - clock-in again → `409 ALREADY_ON_SHIFT` ("on shift since 2026-11-20 08:36")
  - clock-out → `200`
  - clock-out again → `409 NOT_ON_SHIFT`
- Known noise: EF Core logs the refused duplicate insert at Error level before our code handles it. That's expected
  during a race; noted, and not suppressed, because hiding EF save errors globally would also hide real ones.
