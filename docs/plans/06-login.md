# Plan 06: Login and roles

**Status:** done ✅ · **Branch:** `feature/login` · **PRD refs:** F1, D-1, D-7, D-8, EC-14

## Goal
Employees and managers sign in with a username and password. Every later request (Clock In, history, manager screens)
knows **who** is calling and **what role** they have.

## Design

```
POST /api/auth/login { userName, password }
   → check the password against the stored hash
   → OK:   200 { token, expiresAt, employee: { id, displayName, role } }
   → fail: 401 INVALID_CREDENTIALS (the same answer for unknown user / wrong password / deactivated)

Later requests:  Authorization: Bearer <token>   → the server knows: employee id + role
GET /api/auth/me → who am I (proves the token works)
```

| Decision | Choice | Why |
|---|---|---|
| Login method | **JWT** (a signed "digital badge" token), valid **60 min** | Simple with a separate React app; the server stays stateless (PRD D-8). Trade-off: a token can't be revoked early; the short lifetime limits that. |
| Password storage | **Hashed** with ASP.NET Core Identity's `PasswordHasher` (PBKDF2 + salt) | We never store or log a real password. Uses Microsoft's tested implementation, without the full Identity table set we don't need. |
| Failed login message | Always the same generic message | Doesn't reveal whether a username exists. |
| Timing | On an unknown username, still run a dummy hash check | So response time doesn't reveal whether the user exists either. |
| Brute force | **Rate limit:** 5 login attempts per minute per IP → `429` | Stops password guessing. Built-in ASP.NET Core rate limiter. |
| Deactivated users | Can't log in; their data is kept | PRD F1. |
| Roles | `Employee`, `Manager`, in the token | Endpoints use `[Authorize]` / `[Authorize(Roles = "Manager")]`. |
| Signing key | From configuration; dev key in `appsettings.Development.json`, production via environment variable | No real secrets in the repo. |
| Token expiry clock | Server clock | Allowed: infrastructure, not an attendance record (PRD D-7). |
| Registration | **None.** Users are seeded / created by a manager | PRD D-1: internal tool. |

## Seed users (Development only)
Created on startup if the Employees table is empty, so reviewers can log in right away:

| Username | Password | Role |
|---|---|---|
| `manager` | `Manager123!` | Manager |
| `dana` | `Employee123!` | Employee |
| `yossi` | `Employee123!` | Employee |

Listed in the README. These are local demo accounts only.

## Tests (integration: the real app in memory + the real SQL Server, separate test database)
| # | Scenario | Expected |
|---|---|---|
| 1 | Correct username + password | 200 + token + role |
| 2 | Wrong password | 401 `INVALID_CREDENTIALS` |
| 3 | Unknown username | 401, same message as #2 |
| 4 | Deactivated employee | 401, same message |
| 5 | `/me` without a token | 401 |
| 6 | `/me` with a valid token | 200 + the right employee |
| 7 | `/me` with a tampered token | 401 |
| 8 | 6th login attempt within a minute | 429 Too Many Requests |
| 9 | Password stored in the DB | Is a hash, never the plain password |

Tests use their own database (`AttendanceClock_Tests`) so they never touch your dev data.

## Out of scope
Changing or resetting passwords, a manager screen for creating employees (comes with the manager step),
refresh tokens, logout (with JWT, the client simply deletes the token).

## Results (2026-11-20)
- Implemented in `backend/AttendanceClock.Api/Auth/` (+ `Common/ApiProblem.cs`, `Data/DevDataSeeder.cs`).
- **9/9 login tests pass, 19/19 overall** (`AttendanceClock.Tests/Auth/AuthTests.cs`), using the real app in memory and a separate `AttendanceClock_Tests` database that's reset on every run.
- Live check against the running server: dana logs in → token (role Employee, 60 min); `/api/auth/me` with the token →
  `{"id":3,"userName":"dana","displayName":"Dana Cohen","role":"Employee"}`; wrong password → `401 INVALID_CREDENTIALS`; `/me` without a token → `401`.
- Implementation notes:
  - The DB connection string and auth settings are read lazily, so tests can point the app at the test database.
  - The signing key is validated at startup (≥ 32 chars), so a missing production key fails fast instead of silently.
- Hiccup: a PowerShell 5.1 edit garbled non-ASCII characters in this file (`·` → `Â·`). Fixed, and added to CLAUDE.md gotchas.
