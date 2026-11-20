# Plan 09: React screens (employee side)

**Status:** done ✅ · **Branch:** `feature/react-ui` · **PRD refs:** F1–F4, §4.5, D-2, EC-1, EC-3, EC-4, EC-8, EC-10, EC-14

## Goal
What employees actually see and click: **log in**, press **one big Clock In / Clock Out button**, and see **my week**.
Manager screens come in the next step.

## Screens
| Screen | What it shows |
|---|---|
| **Login** | Username + password. Clear messages for a wrong password and for "too many attempts, wait a minute". |
| **Clock** (main) | A live Zurich clock (CET/CEST), my status ("On shift since 08:02 · 3 h 12 min" / "Off shift"), and **one big button** that is Clock In *or* Clock Out. After a punch: "Clocked in at 08:02:11 (timeapi.io)", the **official recorded time** from the server. |
| **My week** | Monday–Sunday, with ◀ ▶ to move between weeks: each day's shifts, daily totals (h:mm), the week total, an "in progress" open shift, and badges for flagged shifts. |

## How it behaves (and why)
| Behaviour | Why |
|---|---|
| **The button is disabled while a punch is in flight**, with a "Recording…" label | Prevents double-clicks (EC-1). The server still guards anyway. |
| **The app never sends a time.** It shows the time the server recorded | PRD D-2: the browser clock can't be trusted (EC-10). |
| **The live clock is cosmetic**: seeded from `/api/time/now`, then ticking locally | Nice to have on screen, but never used for records (PRD §4.5). Re-synced every few minutes. |
| **On `409`** (already on shift / not on shift): show the server's message, then refresh the status | A stale tab or second device just catches up (EC-3, EC-4). |
| **On `503`** (time service down): "Nothing was recorded, please try again" | Fail-closed, explained in plain words (EC-8). |
| **On `401`** (badge expired): go back to the login screen | EC-14. |
| **All times are shown in Zurich time with CET/CEST**, whatever the device's zone | One truth for everyone. |
| Works on a **phone** (big touch target, single column) | Punching from a phone is realistic. |

## Technical choices
| Choice | Why |
|---|---|
| **Vite + React + TypeScript** (required stack) | Fast dev server; TypeScript catches mistakes. |
| **Runs in Docker** (a new `web` container, Node 24 LTS), port **5174** | Vite uses a downloaded, unsigned helper program (esbuild) that Smart App Control would likely block on Windows, the same problem as .NET. Docker also keeps "one command starts everything". |
| **Vite proxy**: the browser calls `/api/...` on 5174, and Vite forwards to the `api` container | No CORS setup needed; the browser only talks to one address. |
| **No extra libraries** for routing or styling: a small tab switch and plain CSS | Three screens don't need a router. Fewer dependencies = less to explain and maintain. |
| **Login token in `localStorage`**, removed on logout or `401` | Survives a page refresh. Trade-off: readable by scripts if the page ever had an XSS hole. We render no raw HTML, so React escapes everything; noted in DECISIONS. |
| **A few unit tests (Vitest)** for the time/duration formatting helpers | The formatting is where subtle bugs hide (h:mm, CET vs CEST). |

## Proof it works
- Start everything with `docker compose up -d`, open http://localhost:5174.
- Walk through it in the browser pane: log in as Dana → Clock In → see the recorded time → refresh the page (still on shift) →
  Clock Out → My week shows the shift → wrong password message → log out.
- Vitest tests pass; the 43 backend tests still pass.

## Out of scope
Manager screens (next step), a production build served by the API, translations (UI is English, per PRD review).

## Results (2026-11-20)
- Built in `frontend/` (Vite 8 + React 19 + TypeScript 5.9), running in a third container `attendance-web`.
  Code: `src/screens/` (Login, Clock, Week), `src/api.ts` (server calls + error codes), `src/session.ts` (login badge),
  `src/time.ts` (Zurich formatting), `src/useZurichClock.ts` (cosmetic live clock).
- **10 Vitest tests pass** (time formatting, incl. the exact CET → CEST switch); type-check clean; 43 backend tests unchanged.
- **Live walkthrough in the browser pane (real timeapi.io):** wrong password → "Invalid username or password." ·
  dana logs in → live clock 12:07 CET · Clock In → "Clocked in at 12:07:44 CET · source timeapi.io", button turns into
  Clock Out · page refresh → still on shift · **clocked out from a second "device"** (API call), then pressed the
  outdated Clock Out → clear message, and the screen refreshed itself to "Off shift" · My week → Mon 16 – Sun 22 Nov with
  the shift · phone-size view OK · log out → stays logged out after refresh.
- **Port changed 5173 → 5174:** the user's older project had a Vite server on 5173, so `localhost:5173` showed the
  *wrong app*. The user chose to move ours rather than stop theirs.
- Wording fix found in the walkthrough: after a stale-tab `409` the message said "Refresh to see your status" although the
  screen already refreshes itself. Now it says "Your status below is up to date."
- Browser console showed only the intentional 401 (wrong password) and 409 (stale tab) responses, no code errors.
