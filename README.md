# Attendance Clock

An attendance system where employees **clock in and out** of shifts, and managers review and correct them.

**The core rule:** every Clock In / Clock Out time comes from an **external time API, in Europe/Zurich time**, never from
the browser's or the server's clock. If the time service is unavailable, the punch is refused rather than recorded with a
time we can't trust.

> 🚧 **Work in progress.** Built step by step; see [Project status](#project-status).

---

## Tech stack
| Part | Technology |
|---|---|
| Backend | ASP.NET Core (.NET 10), C# |
| Database | Microsoft SQL Server 2022, Entity Framework Core 10 |
| Frontend | React (Vite + TypeScript), *coming soon* |
| Time source | [timeapi.io](https://timeapi.io) (primary), [time.now](https://time.now) (backup) |
| Local environment | Docker Compose |

## Run it locally

**You need:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) (running) and git.

```bash
git clone https://github.com/AdielGuetta/AttendanceClock.git
cd AttendanceClock
docker compose up -d
```

The first start takes a few minutes (it downloads SQL Server and the .NET SDK). Then open:

| URL | What you'll see |
|---|---|
| http://localhost:5080/health | `Healthy`: the server is up and can reach the database |
| http://localhost:5080/api/time/now | The current Zurich time from the external time API |

### Demo accounts (created automatically in Development)
| Username | Password | Role |
|---|---|---|
| `manager` | `Manager123!` | Manager |
| `dana` | `Employee123!` | Employee |
| `yossi` | `Employee123!` | Employee |

Log in: `POST http://localhost:5080/api/auth/login` with `{"userName":"dana","password":"Employee123!"}`. The answer
contains a token; send it as `Authorization: Bearer <token>`, e.g. to `GET /api/auth/me`.

**Database setup is automatic:** SQL Server runs in a container, and the server creates and upgrades the tables on
startup (EF Core migrations). There are no manual steps.

Stop with `docker compose down` (data is kept), or `docker compose down -v` to also delete the database.

### Run the tests
```bash
docker compose run --rm --no-deps api sh -c "cd /src && dotnet test"
```

### Without Docker *(alternative)*
With the .NET 10 SDK and any SQL Server: set the connection string in
`backend/AttendanceClock.Api/appsettings.Development.json`, then run `dotnet run --project backend/AttendanceClock.Api`.

## Project status
| Step | Status |
|---|---|
| Requirements (PRD) | ✅ |
| Server + database in Docker | ✅ |
| Database tables and safety rules | ✅ |
| Choosing the time providers (measured) | ✅ |
| Trusted clock: external time, backup, validation, 10 tests | ✅ |
| Login and roles: JWT, hashed passwords, brute-force limit, 9 tests | ✅ |
| Clock In / Clock Out | ⏭️ next |
| React UI | ⏳ |
| Manager dashboard and corrections | ⏳ |

## Documentation
- [docs/PRD.md](docs/PRD.md): requirements, edge cases and design
- [DECISIONS.md](DECISIONS.md): key decisions, edge cases, what was left out, known limitations
- [docs/plans/](docs/plans/): the plan for each step, with its results
- [CLAUDE.md](CLAUDE.md): how Claude Code was guided on this project
