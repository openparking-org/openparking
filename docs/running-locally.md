# Running OpenParking locally

`make dev` is the supported path and needs Docker Desktop. This document covers
the native route for machines without Docker, which is also faster to iterate on
because the API rebuilds in seconds instead of rebuilding an image.

## Prerequisites

| Component | Needed for | Notes |
|---|---|---|
| .NET SDK 8 | API | Newer SDKs work; see *Running on a newer .NET* below |
| Node.js 20+ | Web dashboard | |
| Python 3.11 | AI service | |
| PostgreSQL 16+ | Everything | Homebrew, Postgres.app or a Neon connection string |
| Flutter 3 | Mobile app | Optional unless working on the driver app |

## 1. Database

```bash
brew install postgresql@17
brew services start postgresql@17

createdb openparking
psql -d postgres -c "CREATE ROLE dev LOGIN PASSWORD 'dev' SUPERUSER;"
```

If port 5432 is already taken — macOS ships services that grab it, and other
Postgres installs may be running — set a different port in
`$(brew --prefix)/var/postgresql@17/postgresql.conf` and use it consistently
below:

```conf
port = 5433
```

## 2. Configuration

```bash
cp .env.example .env
```

Two connection variables exist on purpose, because the two runtimes disagree on
format:

- `DATABASE_URL` — a `postgresql://` URI, read by the Python AI service.
- `API_CONNECTION_STRING` — Npgsql keyword syntax (`Host=…;Port=…`), read by
  ASP.NET. Npgsql cannot parse URIs, so this cannot be the same string.

Point both at the same database.

## 3. Apply migrations

```bash
dotnet tool install --global dotnet-ef --version "8.*"
export PATH="$PATH:$HOME/.dotnet/tools"

cd api
export ConnectionStrings__Default="Host=localhost;Port=5433;Database=openparking;Username=dev;Password=dev"
dotnet ef database update --project OpenParking.Infrastructure --startup-project OpenParking.Api
```

The API seeds `SystemSettings` on startup, so pricing and enforcement policy
appear once it has run for the first time.

## 4. Start the services

Each in its own terminal.

```bash
# API -> http://localhost:5000  (Swagger at /swagger)
cd api/OpenParking.Api
ConnectionStrings__Default="Host=localhost;Port=5433;Database=openparking;Username=dev;Password=dev" \
ASPNETCORE_URLS="http://localhost:5000" \
dotnet run
```

```bash
# AI service -> http://localhost:8000  (docs at /docs)
cd ai
python3 -m venv .venv && ./.venv/bin/pip install -r requirements.txt
CF_AI_MODE=mock ./.venv/bin/python -m uvicorn main:app --port 8000
```

```bash
# Dashboard -> http://localhost:3000
cd web
npm install
npm run dev
```

## 5. Demo data

With the API up:

```bash
PGPORT=5433 ./scripts/seed-demo-data.sh
```

This creates two drivers, two zones, twenty slots, and four bookings covering
the active, confirmed, surge-priced and cancelled cases. Bookings are created
through the HTTP API rather than SQL so the fees shown are the ones the real
`FeeCalculationService` produces.

## Running on a newer .NET

The projects target `net8.0`. A machine with only the .NET 9 or 10 runtime
installed will build but refuse to start, because the host looks for a matching
major version. Either install the .NET 8 runtime, or roll forward:

```bash
export DOTNET_ROLL_FORWARD=LatestMajor
```

Leave the target framework alone — CI pins .NET 8, and retargeting would break
the pipeline for everyone else.

## Tests

```bash
cd api && dotnet test OpenParking.sln     # xUnit
cd ai  && ./.venv/bin/python -m pytest    # pytest
cd web && npm run test                    # vitest
```

## Troubleshooting

**`relation "SystemSettings" does not exist`** — migrations have not been
applied. Run step 3.

**`password authentication failed for user "dev"`** — another Postgres is
answering on that port. Check with `lsof -nP -iTCP:5432 -sTCP:LISTEN` and either
stop it or move this instance to its own port.

**`error CS1705` about mismatched EF Core versions** — every
`Microsoft.EntityFrameworkCore.*` and `Npgsql.EntityFrameworkCore.PostgreSQL`
package must resolve to the same version. They are currently aligned at 8.0.8.

**Dashboard shows "Could not load bookings"** — the API is not reachable at
`VITE_API_URL` (default `http://localhost:5000`). Confirm `curl
http://localhost:5000/health` responds.
