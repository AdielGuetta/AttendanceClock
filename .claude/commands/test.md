---
description: Run all automated tests (backend + frontend) in Docker and report the results
---

Run the project's tests without touching the running app, and report the results plainly.

1. Backend (58+ tests, own build folders, the API keeps running):
   `docker compose run --rm tests`
2. Frontend type-check and unit tests:
   `docker exec attendance-web sh -c "npx tsc -b && npx vitest run"`
   (if the web container isn't running: `docker compose run --rm --no-deps web sh -c "npm install && npx tsc -b && npx vitest run"`)

Never run `dotnet` or `npm` directly on Windows (Smart App Control blocks locally built binaries; see CLAUDE.md).

Report: passed/failed counts for each part, and for any failure the test name, the assertion message, and the likely cause.
If tests were added or removed, remind me to update `docs/TESTS.md`.
