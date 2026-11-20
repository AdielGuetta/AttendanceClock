# Plan 08: My shift history and totals

**Status:** done ✅ · **Branch:** `feature/history` · **PRD refs:** F4, D-6, D-10, EC-5, EC-6

## Goal
An employee sees **their own shifts** for a date range (default: this week), with **daily totals** and a **range total**.
This is also the data the React history screen will show.

## Endpoint (requires login)
`GET /api/shifts/mine?from=2026-11-16&to=2026-11-22`. Dates are **Zurich calendar dates**, inclusive.

```json
{
  "from": "2026-11-16", "to": "2026-11-22",
  "totalMinutes": 990,
  "days": [ { "date": "2026-11-16", "totalMinutes": 480, "shiftCount": 1 }, … ],
  "shifts": [ { "id": 1, "zurichDate": "2026-11-16", "clockIn": "…+01:00", "clockOut": "…+01:00", "durationMinutes": 480, "flags": [] }, … ],
  "openShift": null
}
```

## Rules and decisions
| Rule / decision | Why |
|---|---|
| **A shift belongs to the Zurich date of its Clock In** (D-10) | A 22:00 → 06:00 shift counts once, on the start date, never split or double-counted. |
| **Day boundaries are computed with Zurich's official time-zone rules**, not "+1 hour" | On DST days a "day" is 23 or 25 hours long. Midnight in Zurich is converted to the exact instant, so no shift falls on the wrong day. |
| **Totals count closed shifts only.** An open shift is returned separately as `openShift` | Its length isn't known yet. The UI shows it as "in progress". |
| **No dates sent → this week (Mon–Sun, Zurich)**, using the trusted clock to know "today" | One external call per history load is acceptable here. The UI will normally send dates anyway. |
| **Max range 93 days; `from` must be ≤ `to`** → otherwise `400 INVALID_RANGE` | Protects the server from huge queries; ~3 months covers any normal screen. |
| **Only your own shifts.** The employee id comes from the login badge, never from the URL | Nobody can view someone else's history by changing a number. Managers get their own views in the manager step. |
| Days without shifts are **not** listed | Keeps the answer small; the UI can draw empty days itself. |

## Tests (real app + test DB + fake clock)
| # | Scenario | Expected |
|---|---|---|
| 1 | Three shifts on two days | Correct per-day totals and range total |
| 2 | Overnight shift 22:00 → 06:00 | Counted on the start date only |
| 3 | Shift starting 00:30 on the DST day (29 Mar 2026) | Lands on 29 Mar, not 28 Mar |
| 4 | Shifts outside the range | Not included |
| 5 | Another employee's shifts | Never included |
| 6 | Open shift | Returned as `openShift`, not in totals |
| 7 | No dates sent | Uses this week, from the trusted clock |
| 8 | `from` after `to`, or a range > 93 days | 400 `INVALID_RANGE` |
| 9 | Not logged in | 401 |

## Out of scope
Manager views of all employees (manager step), CSV export (nice-to-have N1), overtime rules.

## Results (2026-11-20)
- Implemented in `backend/AttendanceClock.Api/History/` (`HistoryService`, `ShiftsController`), plus two helpers in
  `Time/ZurichTime.cs` (`ToZurichDate`, `StartOfDay`). `ShiftDto` now also carries `zurichDate`.
- **10 history tests (12 cases) pass, 43/43 overall.**
- **Found during the live check:** totals originally added each shift's *rounded-down* minutes, so many short shifts could
  quietly lose time. Changed to add exact durations, then round once; new test 1b (3 × 1 min 40 s = 5 min, not 3).
- Live run (real timeapi.io): `GET /api/shifts/mine` with no dates → `from 2026-11-16, to 2026-11-22` (this Zurich week),
  listing yossi's shift from plan 07. A reversed range → `400 INVALID_RANGE`.
