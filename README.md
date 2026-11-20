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

**You need:** git and Docker, running. This works the same on **Windows, macOS and Linux**:

| OS | Install |
|---|---|
| Windows 10/11 | [Docker Desktop](https://www.docker.com/products/docker-desktop/) (uses WSL 2 under the hood) |
| macOS (Intel or Apple Silicon) | [Docker Desktop](https://www.docker.com/products/docker-desktop/). On Apple Silicon, SQL Server runs under emulation; see [notes](#macos-and-linux-notes) |
| Linux | [Docker Engine](https://docs.docker.com/engine/install/) + the [Compose plugin](https://docs.docker.com/compose/install/linux/) (or Docker Desktop) |

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

Punch (with the token): `POST /api/punches/clock-in`, `POST /api/punches/clock-out`, `GET /api/punches/status`.
No body is sent: the server takes the time from the external time API.

History (with the token): `GET /api/shifts/mine?from=2026-11-16&to=2026-11-22` (Zurich dates; no dates = this week),
with daily and range totals.

**Database setup is automatic:** SQL Server runs in a container, and the server creates and upgrades the tables on
startup (EF Core migrations). There are no manual steps.

Stop with `docker compose down` (data is kept), or `docker compose down -v` to also delete the database.

### Run the tests
```bash
docker compose run --rm --no-deps api sh -c "cd /src && dotnet test"
```

### Without Docker *(alternative, any OS)*
With the [.NET 10 SDK](https://dotnet.microsoft.com/download) and any reachable SQL Server: set the connection string in
`backend/AttendanceClock.Api/appsettings.Development.json`, then run `dotnet run --project backend/AttendanceClock.Api`.
*(On the original dev machine this wasn't possible: Windows Smart App Control blocks locally built programs. That's why
Docker is the main path. See [DECISIONS.md](DECISIONS.md), #9.)*

## How the Docker setup works

`docker-compose.yml` describes two **containers**. A container is an isolated mini-computer (Linux inside) that holds
one program plus everything it needs. They're started from **images**, ready-made templates downloaded once.

```
Your computer (Windows / macOS / Linux)
│
│  http://localhost:5080 ──────────────┐            localhost:1433 (optional: SSMS, Azure Data Studio…)
│                                      ▼                 │
│  ┌─ Docker ─────────────────────────────────────────────┼────────────────────┐
│  │  attendance-api                                      ▼                    │
│  │  image: .NET 10 SDK            ──── "db:1433" ───►  attendance-db         │
│  │  runs: dotnet watch                                  image: SQL Server 2022│
│  │  code: ./backend (live from your disk)               data: volume db-data │
│  │  build output: volumes (api-bin, api-obj…)                                │
│  └───────────────────────────────────────────────────────────────────────────┘
```

**What `docker compose up -d` does:**
1. Downloads the two images, the first time only.
2. Creates a private network, so the containers can reach each other **by name**: the API connects to `db`, not `localhost`.
3. Starts **SQL Server** and waits until its health check passes (it really answers a query).
4. Starts the **API** with `dotnet watch`, which builds the code, applies database migrations, adds the demo accounts,
   and listens on port 5080.

**Good to know**
- **Edit code → the API reloads by itself.** Your `backend/` folder is shared live into the container.
- **Data survives restarts.** It lives in the `db-data` *volume*, a storage area managed by Docker. Only
  `docker compose down -v` deletes it.
- **Ports** connect your computer to the containers: `5080` → the API, `1433` → SQL Server.
- **Build output** (`bin/`, `obj/`) stays inside Docker volumes, so it never mixes with your own OS's files.
- Useful commands: `docker compose ps` (status) · `docker logs attendance-api --tail 50` (API log) ·
  `docker compose restart api`. Docker Desktop shows the same in its **Containers** tab.

### macOS and Linux notes
- **Same commands** on every OS. Use the terminal instead of PowerShell.
- **Apple Silicon (M1–M4):** Microsoft publishes SQL Server images only for Intel/AMD, so `docker-compose.yml` asks for
  `platform: linux/amd64` and Docker Desktop emulates it. In Docker Desktop → Settings → General, keep
  **"Use Rosetta for x86_64/amd64 emulation on Apple Silicon"** on. The first start is slower; after that it works normally.
- **Linux:** if `docker` needs `sudo`, either use `sudo docker compose up -d` or
  [add your user to the `docker` group](https://docs.docker.com/engine/install/linux-postinstall/).
- **Port already in use** (e.g. you already run SQL Server locally on 1433): in `docker-compose.yml` change `"1433:1433"` to
  `"1434:1433"`. The API isn't affected, since it talks to the database inside Docker.
- **Line endings** are normalised by `.gitattributes`, so a clone works the same on every OS.

## Project status
| Step | Status |
|---|---|
| Requirements (PRD) | ✅ |
| Server + database in Docker | ✅ |
| Database tables and safety rules | ✅ |
| Choosing the time providers (measured) | ✅ |
| Trusted clock: external time, backup, validation, 10 tests | ✅ |
| Login and roles: JWT, hashed passwords, brute-force limit, 9 tests | ✅ |
| Clock In / Clock Out / status: race-safe, DST-safe, 12 tests | ✅ |
| Shift history and totals: DST-safe days, exact totals, 10 tests | ✅ |
| React UI | ⏭️ next |
| React UI | ⏳ |
| Manager dashboard and corrections | ⏳ |

## Documentation
- [docs/PRD.md](docs/PRD.md): requirements, edge cases and design
- [DECISIONS.md](DECISIONS.md): key decisions, edge cases, what was left out, known limitations
- [docs/TECH-STACK.md](docs/TECH-STACK.md): pros and cons of each component, and how each affects the user experience.
  **Visual version:** [docs/TECH-STACK.pdf](docs/TECH-STACK.pdf) (diagrams: architecture, Clock In flow, waiting time, concurrency, Docker)
- [docs/TESTS.md](docs/TESTS.md): every edge case (מקרי קצה), how it's handled, and which automated test proves it
- [docs/plans/](docs/plans/): the plan for each step, with its results
- [CLAUDE.md](CLAUDE.md): how Claude Code was guided on this project
