# Plan 11: Alerts for forgotten shifts (> 14 h)

**Status:** done ✅ · **Branch:** `feature/14h-alerts` · **PRD refs:** F7, EC-4

## Why
A shift still open after 14 hours is flagged **"Forgot to clock out?"** (Plan 10), but only inside the manager's Team tab,
so it's easy to miss. We deliberately **don't auto-close** it: that would mean inventing a Clock Out time that came neither
from the time API nor from a person (likely wrong pay). Instead, we make it hard to miss.

## What
| Who | Alert |
|---|---|
| **Employee** | On the Clock screen, an amber banner when their open shift is older than 14 h: *"You've been on shift since Thu 19 Nov, 08:00 (31 h 56 min). Did you forget to clock out?"* plus what to do. |
| **Manager** | A red **count badge** on the Team tab (e.g. "Team ①") whenever someone has a forgotten shift. Refreshed every 2 minutes and when the window regains focus. |

## Decisions
- The employee banner uses the **cosmetic live clock** (server-seeded), not an extra time-API call, because `/status` stays API-free (EC-17).
- The manager badge reuses `/api/admin/on-shift`, whose flag is computed against the **trusted clock**. One call per 2 minutes per open manager screen is negligible.
- Same 14 h threshold as the server (`ShiftService.TooLongThreshold`), mirrored in one frontend constant.
- No e-mail / push notifications: they'd need extra services (SMTP, push keys). Listed in DECISIONS as not done.

## Proof
- Unit tests for the "forgotten?" helper (just under / just over 14 h).
- Browser: a demo forgotten shift for **yossi** → yossi sees the banner; the manager sees "Team ①".

## Results (2026-11-20)
- `frontend/src/time.ts`: `FORGOT_CLOCK_OUT_MINUTES`, `openMinutes`, `isLikelyForgotten` (+3 unit tests: exactly 14 h → quiet,
  14 h 01 → alert, elapsed time across a DST change). Banner in `ClockScreen.tsx`; badge in `App.tsx` (polls
  `/api/admin/on-shift` every 2 min, on focus, and right after a correction in `TeamScreen`).
- **13/13 frontend tests pass**, type-check clean; backend unchanged (58/58).
- Browser: demo forgotten shift for yossi (Thu 07:30) → yossi sees *"Did you forget to clock out? … since Thu 19 Nov,
  07:30 CET (32 h 51 min)"*; the manager sees **"Team ①"** in the top bar. The demo shift is left open for the user to try.
