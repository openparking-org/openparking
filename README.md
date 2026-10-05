# OpenParking 🚗⚡

> **An Integrated Full-Stack & Agentic AI Smart Parking Management System**  
> Developed for **SE3090 — Software Engineering Frameworks (2026)**  
> Hosted by the [openparking-org](https://github.com/openparking-org) GitHub Organization.

[![CI — API](https://github.com/openparking-org/openparking/actions/workflows/ci-api.yml/badge.svg)](https://github.com/openparking-org/openparking/actions/workflows/ci-api.yml)
[![CI — Web](https://github.com/openparking-org/openparking/actions/workflows/ci-web.yml/badge.svg)](https://github.com/openparking-org/openparking/actions/workflows/ci-web.yml)
[![CI — Mobile](https://github.com/openparking-org/openparking/actions/workflows/ci-mobile.yml/badge.svg)](https://github.com/openparking-org/openparking/actions/workflows/ci-mobile.yml)
[![CI — AI Service](https://github.com/openparking-org/openparking/actions/workflows/ci-ai.yml/badge.svg)](https://github.com/openparking-org/openparking/actions/workflows/ci-ai.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## 1. System Overview

OpenParking is a production-grade, multi-agent parking management platform addressing real-time parking spot discovery, dynamic surge pricing, disabled parking permit verification, and automated overstay enforcement.

The system features:
- **Zero Hardcoded Business Policies:** Configurable hourly rates, grace periods, penalty caps, and surge multipliers backed by runtime PostgreSQL storage and `IMemoryCache`.
- **Human-in-the-Loop AI Orchestration:** High-consequence AI decisions (high penalties, surge pricing rates) pause for admin approval via an interactive dashboard before executing.
- **Indoor Blueprint Navigation:** Canvas-based floor plan designer in React with deterministic **A\* pathfinding** in Python delivering turn-by-turn route waypoints to the Flutter driver app.
- **Edge Routing & Protection:** A lightweight Cloudflare Workers API gateway managing CORS, early JWT validation, and reverse proxying to an Oracle Cloud Always-Free ARM VM.

---

## 2. Team & Vertical Slice Ownership

The project is architected following an **OpenMRS-inspired modular service pattern**, where each member owns an independent full-stack vertical slice:

| Member | Component Slice | Core Responsibilities | Distinct Agent Contribution |
|---|---|---|---|
| **Yowun** | **User & Access** | Identity, JWT auth, RBAC (Driver, ParkingAdmin, SystemAdmin), System Settings UI, Permit uploads | **Validator Agent** — Regulatory schema checks & dynamic penalty cap enforcement |
| **Supun** | **Space & Availability** | Zone & slot layout management, SignalR real-time slot occupancy, Blueprint Editor (React), Mobile indoor map view | **Analyzer Agent** — Real-time lot congestion analysis & arrival velocity scoring |
| **Dev** | **Booking & Payment** | Reservation lifecycle, check-in/out session timing, attendant vehicle-number entry and exit, fee calculation & receipts | **Action Agent** — Dynamic surge pricing multipliers & structured penalty proposals |
| **Karuna** | **Enforcement & AI Orchestration** | Overstay detection, LangGraph state machine workflow, React AI approval dashboard, audit logging | **Planner Agent** — Goal decomposition, agent delegation, and human-in-the-loop checkpoint gating |

---

## 3. Technology Stack

| Layer | Technology | Role |
|---|---|---|
| **Backend API** | ASP.NET Core Web API (.NET 10) | Business logic, JWT auth, SignalR hubs, EF Core 8 (Npgsql) |
| **Database** | PostgreSQL 16 (Neon / Local) | Relational schema, transactional sessions, settings store |
| **Web Dashboard** | React 18, TypeScript, Zustand, Vite | Administrative console, blueprint editor, AI approval queue |
| **Mobile App** | Flutter 3, Dart, Riverpod 2 | Driver application, reservation and live session tracking, indoor floor map |
| **AI Subsystem** | Python 3.11, FastAPI, LangGraph | Multi-agent state machine, A* pathfinder, Cloudflare Workers AI |
| **Edge Gateway** | Cloudflare Workers & Cloudflare Tunnel | Global edge routing, CORS, rate limiting, secure VM tunnel |
| **CI / CD** | GitHub Actions & ghcr.io | Path-filtered automated linting, test suites, and Docker image builds |

---

## 4. Repository Structure

```
openparking-org/openparking/
├── api/                              # ASP.NET Core Web API (.NET 8 Clean Architecture)
│   ├── OpenParking.sln
│   ├── OpenParking.Core/             # IParkingModule, ISettingsService, Domain Entities
│   ├── OpenParking.Infrastructure/   # AppDbContext (Npgsql EF Core), Seeders, Cache
│   ├── OpenParking.Api/              # Controllers, SignalR SlotHub, Swagger
│   ├── OpenParking.Tests/            # xUnit tests for CI
│   └── Dockerfile
│
├── web/                              # React Admin Dashboard (React 18 + TS + Zustand + Vite)
│   ├── src/
│   │   ├── modules/space-availability/ # Blueprint floor plan editor
│   │   ├── modules/enforcement/        # Live analytics & AI approval queue
│   │   ├── modules/user-access/        # Dynamic system settings manager
│   │   ├── modules/booking/            # Active reservations & sessions
│   │   └── index.css                   # Vanilla CSS Design System (dark mode, glassmorphism)
│   ├── package.json & vite.config.ts
│   └── index.html
│
├── mobile/                           # Driver Mobile Application (Flutter 3 + Riverpod 2)
│   ├── lib/
│   │   ├── modules/booking/            # Customer reservations, live sessions, and receipts
│   │   ├── modules/space_availability/ # Indoor blueprint navigation screen
│   │   ├── modules/user_access/        # Disability permit upload screen
│   │   ├── modules/enforcement/        # Overstay alert & penalty notice
│   │   └── main.dart                   # Unified Material 3 navigation
│   └── pubspec.yaml
│
├── ai/                               # Agentic AI Subsystem (Python 3.11 + FastAPI + LangGraph)
│   ├── routing/astar.py              # A* indoor shortest path algorithm
│   ├── agents/                       # Validator, Analyzer, Action, and Planner agents
│   ├── tools/config_tools.py         # Dynamic policy fetcher from backend
│   ├── tests/                        # Unit, integration, and golden workflow acceptance tests
│   └── main.py & Dockerfile
│
├── worker/                           # Cloudflare Workers Edge Gateway
│   ├── src/index.ts                  # Edge CORS, JWT pre-check, proxy routing
│   └── wrangler.toml
│
├── docs/                             # Architecture Decision Records (ADRs) & documentation
├── docker-compose.yml                # Local dev composition (API + AI + Postgres)
├── docker-compose.prod.yml           # Production Docker setup for Oracle VM
├── Makefile                          # Developer command automation
└── .env.example                      # Configuration template
```

---

## 5. Quick Start (Local Development)

> **Branch:** day-to-day integration happens on **`develop`**; `main` lags behind.
> After cloning, run `git switch develop` to get the current project.

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) *(or any Docker engine with Compose v2, e.g. Colima)*
- [Node.js 20+](https://nodejs.org/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) *(optional if using Docker)*
- [Python 3.11](https://www.python.org/) *(optional if using Docker)*
- [Flutter 3+](https://flutter.dev/) *(for mobile development)*

### 1. Start Backend & AI Services (One Command)
```bash
# Clone the repository
git clone https://github.com/openparking-org/openparking.git
cd openparking

# Copy environment template
cp .env.example .env

# Run local Docker stack (API + AI + Postgres)
make dev
```
- API will be accessible at: `http://localhost:5000` (Swagger: `http://localhost:5000/swagger`)
- AI Service will be accessible at: `http://localhost:8000` (Docs: `http://localhost:8000/docs`)
- Postgres database running on port: `5432`

### 2. Start the React Admin Dashboard
```bash
cd web
cp .env.example .env   # VITE_API_BASE_URL / VITE_AI_BASE_URL
npm install
npm run dev
```
Access the dashboard at `http://localhost:3000`.

### 3. Run the Flutter Mobile App
```bash
cd mobile
cp .env.example .env   # required: pubspec.yaml bundles .env as an asset
flutter pub get
flutter run
```

### Running the API and AI service without Docker
Useful for faster iteration. Postgres must be running locally.

```bash
# API (http://localhost:5000). Applies migrations and seeds data on startup.
# DATABASE_URL in the root .env must use Npgsql keyword syntax, e.g.
#   DATABASE_URL=Host=localhost;Port=5432;Database=openparking;Username=dev;Password=dev
cd api/OpenParking.Api
dotnet run

# AI service (http://localhost:8000)
cd ai
python3.11 -m venv .venv && ./.venv/bin/pip install -r requirements.txt
CF_AI_MODE=mock ./.venv/bin/python -m uvicorn main:app --port 8000
```

### Seeded accounts (local development only)
The API seeds these on first start. All use the password `Password123!`.

| Email | Role |
|---|---|
| `admin@openparking.local` | SystemAdmin |
| `manager@openparking.local` | ParkingAdmin |
| `driver@openparking.local` | Driver |
| `permit@openparking.local` | Driver (disability permit) |

---

## 6. Makefile Command Reference

| Command | Action |
|---|---|
| `make setup` | Copies `.env`, builds Docker images, and applies EF Core migrations |
| `make dev` | Starts local backend, AI service, and PostgreSQL in Docker |
| `make test` | Executes `.NET test` and `pytest` across backend and AI services |
| `make migrate name=X` | Generates and executes a new Entity Framework Core migration |
| `make logs` | Streams live Docker container logs |
| `make clean` | Shuts down containers and flushes local volume data |

---

## 7. Architecture Decision Records (ADRs)

Key architectural decisions are recorded under [`docs/ADR.md`](docs/ADR.md):
- **ADR-001:** OpenMRS-Inspired Modular Service Architecture
- **ADR-002:** Cloudflare Workers as Edge API Gateway
- **ADR-003:** LangGraph for Stateful Multi-Agent Orchestration
- **ADR-004:** State Management: Zustand (React) & Riverpod 2 (Flutter)

---

## 8. License

Distributed under the MIT License. See [LICENSE](LICENSE) for more information.
