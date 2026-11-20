# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project
**Attendance Clock**: employees clock in and out of shifts; managers review and correct shifts.
Home assignment (deadline 2026-11-22 08:00). The full spec is in [docs/PRD.md](docs/PRD.md). Read it before
designing anything; decisions (`D-n`) and edge cases (`EC-n`) are referenced by ID in code and docs.

**Stack:** ASP.NET Core (.NET 10, controllers) · EF Core 10 · SQL Server 2022 · React (Vite + TypeScript, not built yet).

## The one hard rule
Every Clock In / Clock Out time **must come from an external time API, in Europe/Zurich**.
- Never use `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `TimeProvider.System` or browser time for punch records.
- The client never sends a timestamp for a punch. The server fetches it (PRD D-2).
- If every time provider fails, **reject the punch**. Never fall back to the local clock (PRD D-5).
- The server clock *is* fine for non-attendance things: JWT expiry, logs, `CreatedAtUtc` (PRD D-7).

## Running things: Docker only
Windows **Smart App Control** on the dev machine blocks every locally built .NET assembly, and it can't be
turned off. **Never run `dotnet build/run/test/ef` on Windows.** Run them in the Docker container.

```bash
docker compose up -d                 # start SQL Server (attendance-db) + API (attendance-api, dotnet watch)
docker compose down                  # stop (data kept); add -v to wipe the database
curl http://localhost:5080/health    # "Healthy" = API up and database reachable
docker logs attendance-api --tail 50

# One-off .NET commands: stop the watcher first so two builds don't collide
docker compose stop api
docker compose run --rm --no-deps api sh -c "cd /src && dotnet tool restore && <command>"
docker compose start api
```
- Run tests: `dotnet test` (inside the container, as above).
- Add a migration: `dotnet ef migrations add <Name> --project AttendanceClock.Api --output-dir Data/Migrations`.
  Migrations are applied automatically when the API starts in Development.
- NuGet: pin **10.0.x** versions. Plain `dotnet add package` picks 11.x, which doesn't support net10.0.
- `bin/` and `obj/` live in Docker volumes, separate from Windows. Don't rely on Windows `obj` files.

### Gotchas
- **sqlcmd** needs `-I` (QUOTED_IDENTIFIER ON), or inserts fail because of the filtered unique index. Example:
  `docker exec -i attendance-db sh -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -I -d AttendanceClock'`
- **Git Bash** rewrites `/opt/...` paths passed to `docker exec`. Prefix the command with `MSYS_NO_PATHCONV=1`.
- The repo path contains Hebrew characters (`שעון`). Always quote paths.
- **Windows PowerShell 5.1** reads UTF-8 files as ANSI by default and garbles `·`, `→`, `✅`. Don't edit files through
  `Get-Content`/`Set-Content`; use the Edit/Write tools (or `sed` in Git Bash).
- Integration tests use a separate database, `AttendanceClock_Tests`, deleted and recreated on every run (`ApiFactory`).
- If the API container can't reach nuget.org right after `up`, it's a transient DNS hiccup: `docker restart attendance-api`.

## Code layout
```
backend/
  AttendanceClock.slnx
  dotnet-tools.json                  # local tools (dotnet-ef)
  AttendanceClock.Api/
    Program.cs                       # DI, health checks, dev auto-migrate
    Domain/                          # entities: Employee, Shift (+ enums)
    Data/AppDbContext.cs
    Data/Configurations/             # one IEntityTypeConfiguration per entity (lengths, indexes, constraints)
    Data/Migrations/
    Time/                            # trusted clock: ITrustedClock, FallbackTrustedClock, provider sources, validation
    Controllers/                     # TimeController (GET /api/time/now)
    Auth/                            # login: AuthController, TokenService (JWT), AuthOptions
    Punches/                         # Clock In/Out/status: ShiftService (rules) + PunchesController (thin)
    Common/ApiProblem.cs             # ProblemDetails + stable error "code"
    Data/DevDataSeeder.cs            # demo accounts (Development only)
  AttendanceClock.Tests/             # xUnit. Time/: fake HTTP handler. Auth/, Punches/: real app in memory (ApiFactory) + test DB.
                                     # Punches/ swaps ITrustedClock for FakeTrustedClock (tests only!). DB test classes use
                                     # [Collection(DatabaseCollection.Name)] so they never run in parallel.
docs/PRD.md                          # requirements and decisions
docs/TECH-STACK.md                   # pros/cons + user impact per component (update when a component is added or changed)
DECISIONS.md                         # reviewer-facing summary of decisions, edge cases, limitations (keep brief, update each step)
README.md                            # how to run + project status (update the status table each step)
docs/plans/                          # implementation plans, one per step
docker-compose.yml
```

## Conventions
- **Data rules live in the database too**, not only in C#: one open shift per employee (filtered unique index),
  `ClockOut > ClockIn` (check constraint), `rowversion` for concurrency, no cascade delete of shifts.
- Times are `DateTimeOffset` (`datetimeoffset` in SQL): the instant plus the Zurich offset. Durations are computed on the instant.
- Errors are returned as ProblemDetails with stable error codes (`ALREADY_ON_SHIFT`, `NOT_ON_SHIFT`, `TIME_SOURCE_UNAVAILABLE`, …).
- Comments explain **why**, and reference PRD IDs where relevant.
- When implementation differs from the PRD, update the PRD in the same change and record the reason.

## Git workflow
- One branch per step (`feature/…`, `docs/…`, `setup/…`), merged into `main` with a fast-forward once it works.
- Commit messages: conventional style (`feat:`, `docs:`, `fix:`), a short body explaining why, and the Co-Authored-By trailer.
- Never push to GitHub, or merge, without the user's approval.

## Working with the user
The user is new to programming and will present and explain this project in an interview, so:
- Go **one step at a time**. Before each step, explain what and why in a few plain sentences, then **ask before acting**,
  especially for installs, Windows or system changes, merges and pushes.
- Keep explanations short and jargon-free. Define terms when they first appear. A warehouse or kitchen metaphor helps when it truly fits.
- After each step, show proof that it works (a test, a health check, a query), and say what's not done yet.
- Record notable moments for DECISIONS.md: problems hit, options weighed, where Claude was wrong and had to be corrected.
