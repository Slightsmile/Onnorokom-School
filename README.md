# School Admission System

A first-come-first-served school admission system. Students place a time-limited
hold on a seat in a class/shift, then confirm it by paying the admission fee
within the hold window, or the seat is automatically released when the hold
expires.

## Stack

- **Backend**: C# / .NET 8 minimal API, PostgreSQL (via Npgsql + Dapper)
- **Frontend**: React 18 + Vite
- **Infra**: Docker Compose (PostgreSQL + backend + frontend)

## Running the app

Requires Docker and Docker Compose. No other local installs are needed.

```bash
docker compose up --build
```

This starts three services:

| Service  | URL                     |
|----------|-------------------------|
| Frontend | http://localhost:5173   |
| Backend  | http://localhost:8080   |
| Postgres | localhost:5432          |

Open http://localhost:5173 in a browser. Use the "Admin: Create Class" tab to
create a class/shift, then enter a 17-digit student registration number in the
top bar to apply, pay, or cancel applications from the other tabs.

## Screenshots

**Available Classes**

![Available Classes](<UI/Screenshot 2026-11-10 212109.png>)

**My Applications**

![My Applications](<UI/Screenshot 2026-11-10 212058.png>)

**Admin: Create Class**

![Admin: Create Class](<UI/Screenshot 2026-11-10 212103.png>)

Data persists in a named Docker volume (`db-data`), so `docker compose down`
followed by `docker compose up` keeps previously created classes and holds.
Use `docker compose down -v` to wipe the database.

### Environment variables

| Variable                | Where     | Default                              | Purpose                                   |
|--------------------------|-----------|---------------------------------------|--------------------------------------------|
| `CONNECTION_STRING`      | backend   | set in `docker-compose.yml`           | Npgsql connection string to PostgreSQL     |
| `HOLD_DURATION_SECONDS`  | backend   | `600`                                  | How long a seat hold lasts before expiring |
| `VITE_API_URL`           | frontend  | `http://localhost:8080` (build arg)   | Base URL the frontend uses to call the API |

## Running the backend tests

The backend has 3 automated tests (xUnit), including a concurrency test that
proves seats are never oversold under parallel requests. They run against the
same PostgreSQL instance used by `docker compose`.

With the stack already running (`docker compose up -d`), run the tests from a
throwaway .NET SDK container on the same Docker network as the `db` service:

```bash
docker run --rm --network onnorokom-school_default \
  -e TEST_CONNECTION_STRING="Host=db;Port=5432;Database=admissions;Username=postgres;Password=postgres" \
  -v "$(pwd)/backend:/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/Backend.Tests.csproj
```

(The network name is `<project-dir-name>_default` by default; check with
`docker network ls` if it differs.)

## Project structure

```
backend/
  src/        ASP.NET Core minimal API, Dapper/Npgsql data access, business logic
  tests/      xUnit tests (incl. concurrency test)
  init.sql    Database schema, applied automatically on backend startup
  Dockerfile
frontend/
  src/        React app (class list, my applications, admin create-class screens)
  Dockerfile
docker-compose.yml
```

## API contract

See `ASSUMPTIONS.md` for details on anything the brief left to interpretation.
The backend implements the exact endpoint contract from the assignment brief:
`POST /classes`, `GET /classes`, `GET /classes/{classId}`,
`POST /classes/{classId}/holds`, `GET /holds/{holdId}`,
`POST /holds/{holdId}/confirm`, `DELETE /holds/{holdId}`, `GET /me/holds`,
all identified by the `X-Student-Id` header where applicable.
