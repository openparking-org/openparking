# OpenParking — Integrated Parking Management System
## Architecture & Design Document
**Course:** SE3090 - Software Engineering Frameworks (Assignment 1)
**Group Size:** 4 Members
**Target:** BSc (Hons) in Information Technology — Specializing in SE/AI
**Philosophy:** Open-source, modular, community-driven — OpenMRS-inspired architecture

---

## 1. System Overview

OpenParking is a full-stack, AI-driven parking management system that handles real-time
space allocation, vehicle size matching, dynamic pricing, disabled parking verification,
and automated overstay enforcement. The system follows an **OpenMRS-inspired modular
service-layer pattern** — each component is independently owned, bounded, and pluggable
through a shared `IParkingModule` interface. The architecture targets zero hosting cost
using the **Cloudflare ecosystem** as the primary edge and frontend platform.

### 1.1 User Roles (Three Explicit Roles)

| Role | Platform | Responsibilities |
|---|---|---|
| **Driver** | Flutter mobile app | Search lots, make bookings, scan QR for entry/exit, track session, view penalties |
| **Parking Admin** | React web dashboard | Manage zones/slots, review AI workflow proposals, approve/reject penalties & surge pricing |
| **System Admin** | React web dashboard | User management, system config, audit log review, third-party API key management |

---

## 2. Technology Stack & Core Infrastructure

| Area | Technology | Justification |
|---|---|---|
| **Backend API** | ASP.NET Core Web API (.NET 8) | Mandatory; handles all auth, business rules, validation |
| **Database** | PostgreSQL 16 + Entity Framework Core | Relational integrity for bookings, sessions, audit logs |
| **Web Dashboard** | React 18 (functional, hooks, React Router v6) | Admin/staff interface + AI workflow approval UI |
| **Mobile App** | Flutter 3 / Dart | Driver-facing: bookings, QR scan, GPS navigation, live status |
| **State — React** | Zustand | Lightweight, no boilerplate, easy viva explanation |
| **State — Flutter** | Riverpod 2 | Compile-safe, testable, excellent for async API calls |
| **Agentic AI** | LangGraph (Python) + Cloudflare Workers AI | Stateful graph orchestration; CF Workers AI for cloud inference |
| **AI Ops & RAG** | CF AI Gateway, Agent Tracing, Vectorize | Free-tier caching, rate-limiting, observability, and RAG context |
| **Edge Gateway** | Cloudflare Workers | Rate limiting, JWT pre-validation, routing, CORS at the edge |
| **Version Control & CI/CD** | GitHub + GitHub Actions | Automated test runs on every push/PR to main |

---

## 3. Component Ownership (4-Member Structure)

Each student owns one full vertical slice: backend endpoints, database schema,
React views, Flutter screens, and a distinct Agentic AI agent contribution.

| # | Component | Owner Scope | Distinct AI Contribution |
|---|---|---|---|
| **1** | **User & Access** | Identity, JWT auth, role management, disabled permit verification | **Validator Agent** — checks permit data against regulatory schema, rejects invalid approvals |
| **2** | **Space & Availability** | Zone/slot CRUD, vehicle-size allocation, occupancy tracking, QR code generation | **Analyzer Agent** — queries real-time occupancy & booking velocity data via allow-listed tools |
| **3** | **Booking & Payment** | Reservation logic, session lifecycle, Mapbox navigation, Resend email confirmations | **Action Agent** — generates structured JSON penalty proposals or surge-price multipliers |
| **4** | **Enforcement & AI Orchestration** | Overstay detection, penalty lifecycle, full LangGraph workflow wiring, audit logs | **Planner Agent** — receives domain objectives, creates structured multi-step plans, delegates |

### Shared Module Interface (OpenMRS-Inspired)

Every component's primary service implements:

```csharp
public interface IParkingModule
{
    string ModuleName { get; }
    Task<HealthStatus> HealthCheckAsync();
    Task<ModuleMetrics> GetMetricsAsync();
}
```

This enforces bounded ownership and enables the modular architecture justification in the ADR.

---

## 4. Third-Party Integrations

### 4.1 Mapbox (Maps & Navigation) — Free Tier: 50,000 map loads/month
- **Business purpose:** Drivers need turn-by-turn navigation to a parking lot and a visual map of available zones.
- **Flutter usage:** `mapbox_maps_flutter` SDK — displays nearest open lots on a live map, routes driver to entrance.
- **ASP.NET Core usage:** Reverse geocoding for lot addresses; coordinates stored in PostgreSQL.
- **Credentials:** Stored in `MAPBOX_ACCESS_TOKEN` environment variable; never committed to Git.

### 4.2 Resend (Transactional Email) — Free Tier: 3,000 emails/month
- **Business purpose:** Booking confirmations, overstay warnings, penalty notifications, and admin approval alerts.
- **ASP.NET Core usage:** `EmailService` calls Resend REST API; called internally — never directly from clients.
- **Error handling:** Timeout (5s), retry (x2), dead-letter log in PostgreSQL if all attempts fail.
- **Credentials:** Stored in `RESEND_API_KEY` environment variable.

---

## 5. Flutter Device Features (Mandatory Requirement)

### 5.1 QR Code Scanner — `mobile_scanner` package
- **Flow:** Driver arrives → opens Flutter app → taps "Scan to Enter" → scans QR on gate terminal → ASP.NET Core validates booking + activates session → gate opens (or ANPR simulator triggers).
- **Exit flow:** Driver scans exit QR → backend calculates duration + fee → session closed → receipt pushed to app.

### 5.2 GPS & Mapbox Map
- **Flow:** Driver taps "Find Parking Near Me" → Flutter requests device GPS → Mapbox displays nearest available lots with live availability counts → driver taps lot → navigation starts.

---

## 6. High-Level Architecture (OpenMRS-Inspired Modular Pattern)

```
+--------------------------------------------------------------------------+
|                        CLOUDFLARE EDGE LAYER                             |
|   Cloudflare Workers  (API Gateway: rate limiting, JWT pre-check, CORS)  |
|   Cloudflare Pages    (React Admin Dashboard — global CDN)               |
|   Cloudflare Workers AI (LLM inference: Llama 3.1 / Qwen2.5)            |
|   Cloudflare AI Gateway (Analytics, Caching, Rate-limiting for LLMs)    |
|   Cloudflare Vectorize  (RAG context for parking regulations)           |
|   Cloudflare R2       (APK hosting, QR images, uploaded permits)         |
|   Cloudflare Tunnel   (securely connects Oracle VM to CF network)        |
+----------------------------------+---------------------------------------+
                                   | HTTPS (all traffic through CF)
+----------------------------------v---------------------------------------+
|               ORACLE CLOUD FREE VM (ARM — Always Free)                   |
|  +-----------------------------------------------------------------------+|
|  |  ASP.NET Core Web API (.NET 8)                                       ||
|  |  Controllers -> DTOs -> IParkingModule services -> EF Core           ||
|  |  JWT Auth | Role-Based Authorization | Swagger | Health endpoint     ||
|  +---------------------+-----------------------+-----------------------+ ||
|                        |                       |                         |
|  +---------------------v---------+  +----------v--------------------+   |
|  |   Neon.tech — PostgreSQL 16   |  |  Python LangGraph Service     |   |
|  |   (Serverless, free 0.5 GB)   |  |  4 Agents | CF Workers AI     |   |
|  |   EF Core Migrations + Seeds  |  |  Persisted state in Neon      |   |
|  +-------------------------------+  +-------------------------------+   |
+--------------------------------------------------------------------------+
```

### Request Flow
1. **Flutter / React** → Cloudflare Workers (edge gateway) → ASP.NET Core API (Oracle VM via CF Tunnel)
2. **ASP.NET Core** reads/writes PostgreSQL (Neon) and triggers LangGraph when AI workflows fire
3. **LangGraph** calls Cloudflare Workers AI for inference; writes workflow state back to Neon
4. **React admin** polls ASP.NET for pending approvals; clicks Approve/Reject → ASP.NET finalizes → Flutter receives updated status

---

## 7. Cloudflare Workers — Edge Gateway Design

The Cloudflare Worker acts as a **thin, stateless API gateway**. It does NOT contain business logic.

```typescript
// worker.ts — deployed to Cloudflare Workers (free: 100k req/day)
export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    // 1. JWT signature pre-check (block obviously invalid tokens at edge)
    // 2. Rate limiting per IP using CF's built-in rate limit API
    // 3. CORS headers injection
    // 4. Proxy to ASP.NET Core via Cloudflare Tunnel URL
    const apiUrl = env.API_ORIGIN + new URL(request.url).pathname;
    return fetch(apiUrl, { ...request, headers: { ...request.headers } });
  }
}
```

**Why Workers as gateway (for ADR):**
- Protects the Oracle VM from direct internet exposure
- Handles CORS, rate limiting, and DDoS at the edge — zero load on .NET
- `CF_API_ORIGIN` env var points to the internal Cloudflare Tunnel URL
- Keeps ASP.NET Core as the **only authoritative backend** (spec requirement)

---

## 8. Agentic AI Subsystem: Dynamic Pricing & Overstay Workflow

The AI subsystem executes a stateful, multi-step LangGraph workflow. It is not a chatbot —
it uses defined allow-listed tools, validates structured inputs/outputs, persists state,
and pauses for human authorization before any high-impact action.

### 8.1 The Four Distinct Agents

```
[Trigger] --> [PLANNER] --> [ANALYZER] --> [ACTION] --> [VALIDATOR]
                                                              |
                                                    Pass -----+---- Fail
                                                      |              |
                                               [PAUSE: Human]  [Safe Fail]
                                               Approve/Reject
                                                      |
                                              [FINALIZE + AUDIT]
```

| Agent | Student | Responsibility | Tools Allowed |
|---|---|---|---|
| **Planner** | Student 4 | Receives objective string, decomposes into structured `ExecutionPlan` with ordered steps | `create_plan`, `log_objective` |
| **Analyzer** | Student 2 | Queries occupancy, booking velocity, user violation history | `get_occupancy_stats`, `get_user_violation_history`, `get_zone_traffic` |
| **Action** | Student 3 | Produces deterministic `PenaltyProposal` or `SurgePriceProposal` JSON | `calculate_penalty`, `calculate_surge_multiplier` |
| **Validator** | Student 1 | Schema + business-rule checks (max fine cap, legal surge limit, permit validity) | `validate_schema`, `check_regulatory_limits` |

### 8.2 Persisted Workflow State (PostgreSQL — Neon)

```sql
CREATE TABLE agent_workflow_runs (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  objective     TEXT NOT NULL,
  workflow_type VARCHAR(50) NOT NULL,  -- 'OVERSTAY' | 'SURGE_PRICING'
  plan          JSONB,
  current_step  VARCHAR(50),
  step_results  JSONB,
  status        VARCHAR(30) NOT NULL,  -- RUNNING | AWAITING_APPROVAL | APPROVED | REJECTED | FAILED
  approval_by   UUID REFERENCES users(id),
  approved_at   TIMESTAMPTZ,
  error_log     JSONB,
  created_at    TIMESTAMPTZ DEFAULT NOW(),
  updated_at    TIMESTAMPTZ DEFAULT NOW()
);
```

### 8.3 Workflow Execution & Human Approval
1. **Trigger:** Session expires in PostgreSQL (ASP.NET Core `BackgroundService`), or occupancy crosses surge threshold.
2. **Initiation:** `POST /api/agent/workflows` — ASP.NET Core creates workflow record, calls Python LangGraph service internally.
3. **Execution:** LangGraph routes state through Planner → Analyzer → Action → Validator.
4. **Pause:** Validator passes → LangGraph emits `AWAITING_APPROVAL`; state saved to Neon.
5. **React Notification:** Admin dashboard polls `GET /api/agent/workflows/pending`; displays full execution summary.
6. **Decision:** Admin clicks Approve or Reject → `POST /api/agent/workflows/{id}/approve` or `/reject`.
7. **Finalization:** ASP.NET Core writes penalty/rate change to PostgreSQL, triggers Resend email to driver, updates Flutter app status.

### 8.4 ANPR Hardware — Simulator (No Demo Risk)
The ESP8266/OpenCV ANPR layer is implemented as a **Python simulator service**:
```
POST /simulate/entry  { "plate": "WP CBA-1234", "zone": "A" }
POST /simulate/exit   { "plate": "WP CBA-1234" }
```
This satisfies the hardware design without live hardware failure risk during evaluation.
The actual ESP8266 integration is documented as an optional hardware extension.

---

## 9. Database Design Principles

- **Normalized schema** (3NF) with explicit foreign keys and cascade rules.
- **Audit fields** on every table: `created_at`, `updated_at`, `created_by`.
- **Indexes** on all foreign keys, booking date ranges, and filtered columns (`status`, `zone_id`).
- **Transactions** for booking creation (slot reservation + session open) and penalty finalization.
- **Migrations** via EF Core (`dotnet ef migrations add`) — never manual schema edits.
- **Seed data** for all user roles, zones, and slots for repeatable demo setup.

---

## 10. Deployment Architecture (100% Free — Cloudflare Ecosystem)

| Component | Service | Cost |
|---|---|---|
| React Web App | **Cloudflare Pages** | Free |
| Edge API Gateway | **Cloudflare Workers** | Free (100k req/day) |
| ASP.NET Core + Python AI | **Oracle Cloud Free VM** (ARM, 4 cores, 24GB RAM) | Always Free |
| PostgreSQL | **Neon.tech** (serverless, 0.5 GB) | Free |
| AI Model Inference | **Cloudflare Workers AI** (Llama 3.1 8B / Qwen2.5) | Free (10k neurons/day) |
| AI Analytics & Caching | **Cloudflare AI Gateway** | Free |
| Agent Observability | **Cloudflare Agent Tracing** | Free |
| RAG Vector Database | **Cloudflare Vectorize** | Free |
| Tunnel (VM → CF network) | **Cloudflare Tunnel** | Free |
| APK + asset hosting | **Cloudflare R2** (10 GB) | Free |
| CI/CD | **GitHub Actions** | Free (public repo) |

**Total monthly cost: $0**

### Startup Order (for evaluators)
```
# 1. Neon: Already running (serverless, zero startup)
# 2. Oracle VM — SSH in, then:
docker compose up -d   # starts ASP.NET Core + Python LangGraph + cloudflared
# 3. GitHub push to main -> Cloudflare Pages auto-deploys React
# 4. Flutter APK — sideload from R2 link in README
```

---

## 11. Developer Onboarding — Easy Setup

### 11.1 One-Command Local Start (Docker Compose)

```yaml
# docker-compose.yml
services:
  api:
    build: ./api
    ports: ["5000:8080"]
    environment:
      - ConnectionStrings__Default=${DATABASE_URL}
      - JWT__Secret=${JWT_SECRET}
      - MAPBOX_ACCESS_TOKEN=${MAPBOX_ACCESS_TOKEN}
      - RESEND_API_KEY=${RESEND_API_KEY}
      - AI_SERVICE_URL=http://ai:8000
    depends_on: [db]

  ai:
    build: ./ai
    ports: ["8000:8000"]
    environment:
      - CF_ACCOUNT_ID=${CF_ACCOUNT_ID}
      - CF_AI_TOKEN=${CF_AI_TOKEN}
      - DATABASE_URL=${DATABASE_URL}

  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: openparking
      POSTGRES_USER: dev
      POSTGRES_PASSWORD: dev
    ports: ["5432:5432"]
    volumes: ["pgdata:/var/lib/postgresql/data"]

volumes:
  pgdata:
```

```bash
# Full local setup — 3 commands:
cp .env.example .env          # fill in your keys
docker compose up -d          # starts API + AI service + local Postgres
cd api && dotnet ef database update  # runs migrations + seed data
```

### 11.2 Environment Variables Template (.env.example)

```
# Database
DATABASE_URL=postgresql://dev:dev@db:5432/openparking

# Auth
JWT_SECRET=change-me-minimum-32-chars
JWT_EXPIRY_HOURS=24

# Third-party
MAPBOX_ACCESS_TOKEN=pk.eyJ1...
RESEND_API_KEY=re_...

# Cloudflare AI
CF_ACCOUNT_ID=your-cf-account-id
CF_AI_TOKEN=your-cf-ai-api-token

# Cloudflare Tunnel (production only)
CF_TUNNEL_TOKEN=your-tunnel-token
```

### 11.3 Makefile

```makefile
setup:   ## Initial setup: copy env, build, migrate, seed
	cp .env.example .env && docker compose build && \
	docker compose run api dotnet ef database update

dev:     ## Start all services locally
	docker compose up

test:    ## Run all tests (backend + AI)
	docker compose run api dotnet test
	docker compose run ai pytest

migrate: ## Create and apply a new EF migration (usage: make migrate name=AddPenalty)
	docker compose run api dotnet ef migrations add $(name) && \
	docker compose run api dotnet ef database update

logs:    ## Tail all service logs
	docker compose logs -f
```

### 11.4 Dev Container (VS Code / GitHub Codespaces zero-install)

A `.devcontainer/devcontainer.json` provides a fully pre-configured environment:
- .NET 8 SDK, Python 3.11, Flutter 3, Node 20 pre-installed
- PostgreSQL sidecar container auto-started
- VS Code extensions: C# DevKit, Flutter, Pylance, REST Client
- Ports forwarded: API (5000), AI service (8000), Postgres (5432)
- `postCreateCommand: make setup` — one click to full working state

---

## 12. CI/CD — GitHub Actions

```yaml
# .github/workflows/ci.yml — runs on every push and pull request to main
on: [push, pull_request]
jobs:
  backend-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16-alpine
        env: { POSTGRES_PASSWORD: test, POSTGRES_DB: openparking_test }
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.x' }
      - run: dotnet restore ./api
      - run: dotnet build ./api --no-restore
      - run: dotnet test ./api --no-build --logger trx

  ai-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with: { python-version: '3.11' }
      - run: pip install -r ai/requirements.txt
      - run: pytest ai/tests/

  react-build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: npm ci --prefix web && npm run build --prefix web
```

Cloudflare Pages auto-deploys React on every push to `main` via its native GitHub integration.

---

## 13. Architecture Decision Records (ADR Summaries)

| # | Decision | Choice | Rationale |
|---|---|---|---|
| ADR-001 | React state management | **Zustand** | No boilerplate, easy to test, simple to explain at viva |
| ADR-002 | Flutter state management | **Riverpod 2** | Compile-safe, scales with async API calls, production-proven |
| ADR-003 | Agentic AI framework | **LangGraph** | Natively supports stateful interruptible graphs — required for human-in-the-loop |
| ADR-004 | AI inference (cloud) | **Cloudflare Workers AI** | Free tier, no GPU required, globally distributed, avoids Ollama instability |
| ADR-005 | Edge gateway | **Cloudflare Workers** | Offloads CORS/rate-limiting from .NET VM; spec requires ASP.NET as sole business backend |
| ADR-006 | Backend hosting | **Oracle Cloud Free VM** (ARM) | Always-free ARM instance; full Docker; no cold starts vs Render free tier |
| ADR-007 | Database hosting | **Neon (PostgreSQL)** | Serverless PostgreSQL; instant dev/test branching; EF Core provider available |
| ADR-008 | Modular architecture | **OpenMRS-inspired** | Proven pattern for community-driven modular systems; maps to 1-component-per-student rule |

---

## 14. Security Considerations

- **Secrets:** All API keys in environment variables; `.env` in `.gitignore`; GitHub Actions uses Repository Secrets.
- **JWT:** Signed with HS256; validated at Cloudflare Worker (signature check) and ASP.NET Core (full claims). Flutter stores tokens via `flutter_secure_storage`.
- **Database:** Neon connection uses `sslmode=require`. DB user has least-privilege role (no DROP/CREATE in production).
- **AI inputs:** All LangGraph tool inputs validated against JSON Schema before execution. Prompt injection mitigated by parameterized tool calls — no string interpolation in prompts.
- **Agent tools:** Strict allow-list — agents cannot call arbitrary endpoints; each tool is a typed Python function with validated input/output contracts.
- **Cloudflare Workers:** Rate limiting (100 req/min per IP). Workers proxy only to the internal Tunnel URL — Oracle VM is never directly internet-exposed.
- **R2 Storage:** Permit images and APK served via pre-signed URLs; no public bucket access.

---

## 15. GitHub Organization — Full Setup Guide

The project is hosted under the `openparking-org` GitHub Organization. Using an Org
(instead of a personal repo) unlocks team-level features that directly support the
assignment's collaboration, CI, and individual-contribution requirements.

### 15.1 Repository Structure

```
openparking-org/openparking/          ← monorepo (single repo, easier for evaluators)
├── api/                              ← ASP.NET Core Web API
├── web/                              ← React Admin Dashboard
├── mobile/                           ← Flutter App
├── ai/                               ← Python LangGraph AI service
├── worker/                           ← Cloudflare Workers edge gateway
├── docs/                             ← ADRs, ER diagrams, architecture docs
├── .github/
│   ├── workflows/
│   │   ├── ci-api.yml                ← Backend CI (path-filtered)
│   │   ├── ci-web.yml                ← React CI (path-filtered)
│   │   ├── ci-mobile.yml             ← Flutter CI + APK build
│   │   ├── ci-ai.yml                 ← Python/LangGraph CI
│   │   ├── deploy-worker.yml         ← Auto-deploy Cloudflare Worker on main push
│   │   └── deploy-vm.yml             ← Build images → ghcr.io → SSH deploy to Oracle VM
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md
│   │   └── feature.md                ← Links tasks to assignment marking criteria
│   ├── CODEOWNERS                    ← Auto-assign PR reviewers by component path
│   ├── pull_request_template.md      ← Checklist: tests, no secrets, viva evidence
│   └── dependabot.yml                ← Weekly security updates for all 5 stacks
├── .devcontainer/
│   └── devcontainer.json             ← GitHub Codespaces / VS Code dev environment
├── docker-compose.yml                ← Local dev (API + AI + Postgres)
├── docker-compose.prod.yml           ← Production (uses ghcr.io images)
├── Makefile                          ← make dev | test | migrate | logs
└── .env.example
```

### 15.2 GitHub Organization Features Used

| Feature | How It's Used | Benefit |
|---|---|---|
| **Org-level Secrets** | `CF_API_TOKEN`, `ORACLE_VM_SSH_KEY`, `RESEND_API_KEY` stored once at org level | All students' branches share secrets without re-entering; secrets never committed |
| **Org-level Variables** | `API_BASE_URL`, `CF_ACCOUNT_ID` stored as non-secret org vars | Safely used in workflow `env:` blocks and Flutter build args |
| **GitHub Environments** | `production` environment with required reviewer approval | Mirrors human-in-the-loop pattern; no accidental prod deploys |
| **GitHub Container Registry (ghcr.io)** | Docker images for API + AI service built in CI and pulled on Oracle VM | Free storage (500 MB free for org), no Docker Hub rate limits |
| **CODEOWNERS** | Each student auto-assigned as PR reviewer for their component paths | Creates auditable review trail per student — direct marking evidence |
| **Branch Protection (main)** | Require: passing CI, 1 approving review, no direct pushes | Enforces code review; protects demo branch from broken code |
| **Branch Protection (develop)** | Require: passing CI only | Safe integration branch for merging feature work |
| **GitHub Projects (Org-level)** | Kanban board: Backlog → In Progress → In Review → Done | Visual task allocation proof — one screenshot shows individual contribution |
| **GitHub Codespaces** | Each student opens repo in Codespaces — zero local install | Identical environment for all 4 students; devcontainer.json auto-configures everything |
| **Dependabot** | Weekly PRs for NuGet, npm, pip, pub, Docker, GitHub Actions | Security patches applied automatically; no stale dependencies |
| **Code Scanning (CodeQL)** | SAST scanning on every PR | Catches security issues early; demonstrates professional practice |
| **Secret Scanning** | Org-wide push protection | Blocks any accidental commit of API keys or tokens |
| **GitHub Actions (path filtering)** | Each CI workflow only runs when its directory changes | Fast CI — pushing Flutter code doesn't re-run the .NET tests |

### 15.3 Org-Level Secrets to Configure

Go to **Organization Settings → Secrets and variables → Actions** and add:

| Secret Name | Value | Used In |
|---|---|---|
| `CF_API_TOKEN` | Cloudflare API token (Workers:Edit) | `deploy-worker.yml` |
| `CF_ACCOUNT_ID` | Cloudflare account ID | `deploy-worker.yml` |
| `ORACLE_VM_HOST` | Oracle VM public IP | `deploy-vm.yml` |
| `ORACLE_VM_USER` | SSH user (`ubuntu` or `opc`) | `deploy-vm.yml` |
| `ORACLE_VM_SSH_KEY` | Private SSH key (PEM, multiline) | `deploy-vm.yml` |
| `RESEND_API_KEY` | Resend email API key | API service (env var) |
| `MAPBOX_ACCESS_TOKEN` | Mapbox public token | API + Flutter (env var) |
| `JWT_SECRET` | 32+ char random string | API service (env var) |
| `NEON_DATABASE_URL` | Neon PostgreSQL connection string | API + AI service |

### 15.4 GitHub Environments Setup

Go to **Repository Settings → Environments** and create:

**`production` environment:**
- ☑ Required reviewers: add all 4 team members (anyone can approve)
- ☑ Wait timer: 0 minutes
- ☑ Deployment branches: `main` only
- This gates `deploy-worker.yml` and `deploy-vm.yml` — a team member must click
  "Approve" in the GitHub UI before code ships to production. This directly mirrors
  the human-in-the-loop pattern in the Agentic AI subsystem.

### 15.5 GitHub Container Registry (ghcr.io) Flow

```
Developer pushes to main
         │
         ▼
  GitHub Actions (deploy-vm.yml)
         │
    ┌────┴─────────────────┐
    │  Build Docker image   │
    │  docker build ./api   │
    └────────────────────── ┘
         │
    ┌────▼─────────────────────────────┐
    │  Push to ghcr.io (free, org pkg) │
    │  ghcr.io/openparking-org/api:latest │
    └──────────────────────────────────┘
         │
    ┌────▼────────────────────────────────────┐
    │  SSH → Oracle VM                         │
    │  docker pull ghcr.io/openparking-org/api │
    │  docker compose up -d --no-deps api      │
    └──────────────────────────────────────────┘
```

This means the Oracle VM **never needs the source code** — it just pulls the pre-built
image. New deployments take ~30 seconds on the VM side.

### 15.6 GitHub Codespaces — Zero-Install Dev Environment

Every team member can open the repo in a browser and get a full dev environment:

```
1. Go to github.com/openparking-org/openparking
2. Press the . key (or change .com to .dev in the URL) → VS Code in browser
   — OR —
   Click Code → Codespaces → Create codespace on [your branch]
3. Wait ~2 minutes for devcontainer to build
4. All tools installed: .NET 8, Python 3.11, Flutter, Node 20, Docker
5. make setup runs automatically → migrations applied, seed data loaded
6. React dev server: npm run dev --prefix web → auto-opens in browser tab
```

**Free allowance:** 60 core-hours/month per GitHub account (120 for Pro).
A 2-core Codespace = 30 hours/month of free development time per student.

### 15.7 Branch Strategy

```
main            ← always deployable; protected; requires PR + CI + review
  └── develop   ← integration branch; CI required; direct push blocked
        ├── feature/student1/jwt-auth
        ├── feature/student2/slot-allocation
        ├── feature/student3/booking-flow
        └── feature/student4/overstay-detection
```

Each student works on `feature/<student>/<task>` branches, opens PRs to `develop`,
gets reviewed (CODEOWNERS auto-assigns), and merges. `develop → main` is a separate
PR that triggers the production deploy pipeline.

### 15.8 GitHub Projects — Task Board

Create a Project at the **Org level** (not repo level) so it spans all future repos:

**Columns:**
```
📋 Backlog  →  🔄 In Progress  →  👀 In Review  →  ✅ Done
```

**Labels to create in the repo:**
```
component:user-access       (purple)
component:space              (blue)
component:booking            (green)
component:enforcement        (red)
layer:api                    (dark)
layer:react                  (cyan)
layer:flutter                (sky)
layer:ai                     (orange)
priority:high                (red)
priority:medium              (yellow)
assignment:evidence          (gold)   ← tag issues that generate marking evidence
```

Using `assignment:evidence` on issues and linking them in PRs creates a clean paper
trail: lecturer can see exactly which issues → commits → PRs each student owns.

---

## 16. Indoor Blueprint Map System (A* Pathfinding)

**Owner:** Student 2 — Space & Availability Component

Rather than relying on GPS (which fails indoors and underground), OpenParking uses a
**Blueprint Image + A* Pathfinding** approach. Admins upload a floor-plan image and
draw their parking layout in the browser. Drivers get a live, routed indoor map in
the Flutter app — no GPS hardware required.

### 16.1 Core Concept

```
Admin uploads floor plan image
         │
         ▼
React-Konva canvas editor (drag rectangles = slots, draw waypoints = routing grid)
         │
         ▼
ASP.NET Core saves FloorPlan + Slot positions as JSON → PostgreSQL
         │
         ▼ (driver books a slot)
Python service runs A* on the waypoint graph → returns ordered path array
         │
         ▼
Flutter InteractiveViewer renders blueprint + colored slot boxes + route line
```

---

### 16.2 Database Schema

```sql
-- One record per physical floor of a parking lot
CREATE TABLE floor_plans (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  zone_id        UUID NOT NULL REFERENCES zones(id) ON DELETE CASCADE,
  floor_number   INT NOT NULL DEFAULT 1,
  label          VARCHAR(100),                    -- e.g. "Ground Floor", "Level 2"
  image_url      TEXT NOT NULL,                   -- Cloudflare R2 pre-signed URL
  image_width_px INT NOT NULL,                    -- original image dimensions
  image_height_px INT NOT NULL,
  -- Routing grid stored as a GeoJSON-like adjacency list
  -- Array of { id, x, y, neighbors: [id, ...] }
  waypoint_graph JSONB NOT NULL DEFAULT '[]',
  -- Entrance/exit waypoint IDs for A* start nodes
  entry_waypoints JSONB NOT NULL DEFAULT '[]',   -- [{ waypointId, label }]
  created_at     TIMESTAMPTZ DEFAULT NOW(),
  updated_at     TIMESTAMPTZ DEFAULT NOW()
);

-- Each parking slot references its position on the floor plan canvas
ALTER TABLE parking_slots ADD COLUMN floor_plan_id UUID REFERENCES floor_plans(id);
ALTER TABLE parking_slots ADD COLUMN canvas_x      FLOAT;   -- px from top-left
ALTER TABLE parking_slots ADD COLUMN canvas_y      FLOAT;
ALTER TABLE parking_slots ADD COLUMN canvas_w      FLOAT;   -- width in px
ALTER TABLE parking_slots ADD COLUMN canvas_h      FLOAT;
ALTER TABLE parking_slots ADD COLUMN canvas_angle  FLOAT DEFAULT 0;  -- rotation °
ALTER TABLE parking_slots ADD COLUMN nearest_waypoint_id TEXT;       -- A* snap point
```

---

### 16.3 React — Admin Floor Plan Editor (React-Konva)

The React dashboard's **Floor Plan Editor** lets Parking Admins draw their lot layout
visually without any GIS or technical knowledge.

**Libraries:**
- `react-konva` — HTML5 Canvas React bindings (draw, drag, resize shapes)
- `konva` — underlying canvas engine
- `react-konva-utils` — image loading helper

**Editor Features:**

| Tool | Interaction | Saves as |
|---|---|---|
| Upload floor plan | Admin clicks "Upload Image" → R2 presigned upload | `floor_plans.image_url` |
| Draw parking slot | Admin drags a rectangle over a space | `parking_slots.canvas_x/y/w/h` |
| Name / type slot | Click slot → sidebar shows ID, type (standard/disabled/EV) | `parking_slots.slot_type` |
| Draw waypoints | Admin clicks empty areas to drop routing nodes | `floor_plans.waypoint_graph[].x/y` |
| Connect waypoints | Admin draws lines between nodes to define driveable paths | `waypoint_graph[].neighbors` |
| Mark entrance | Admin right-clicks a waypoint → "Set as Entry Point" | `floor_plans.entry_waypoints` |
| Multi-floor | Tabs at top: "Ground", "Level 1", "Level 2" → each is a separate `floor_plan` row | Multiple `floor_plans` per zone |

**Simplified component structure:**

```tsx
// web/src/modules/space-availability/FloorPlanEditor.tsx
import { Stage, Layer, Image, Rect, Circle, Line, Text } from 'react-konva';

export function FloorPlanEditor({ floorPlanId }: Props) {
  const [slots, setSlots]         = useState<SlotShape[]>([]);
  const [waypoints, setWaypoints] = useState<Waypoint[]>([]);
  const [tool, setTool]           = useState<'slot' | 'waypoint' | 'connect' | 'select'>('select');

  // Mouse down on empty canvas → create new slot or waypoint depending on active tool
  const handleStageMouseDown = (e: KonvaEventObject<MouseEvent>) => { ... };

  // Drag-end on a slot → update its canvas_x/y position in local state
  const handleSlotDragEnd = (id: string, x: number, y: number) => { ... };

  // Save button → PUT /api/floor-plans/{id} with full slot + waypoint payload
  const handleSave = async () => {
    await api.put(`/floor-plans/${floorPlanId}`, { slots, waypoints });
  };

  return (
    <div className="editor-layout">
      <EditorToolbar activeTool={tool} onToolChange={setTool} onSave={handleSave} />
      <Stage width={canvasWidth} height={canvasHeight} onMouseDown={handleStageMouseDown}>
        <Layer>
          {/* Blueprint image as background */}
          <Image image={blueprintImg} opacity={0.85} />
          {/* Routing grid lines */}
          {edges.map(edge => <Line key={edge.id} points={edge.points} stroke="#60a5fa" />)}
          {/* Waypoint nodes */}
          {waypoints.map(wp => <Circle key={wp.id} x={wp.x} y={wp.y} radius={6} fill="#3b82f6" />)}
          {/* Parking slot rectangles */}
          {slots.map(slot => (
            <Rect key={slot.id} x={slot.x} y={slot.y} width={slot.w} height={slot.h}
              fill={slot.isOccupied ? '#ef4444' : '#22c55e'} opacity={0.6}
              draggable onDragEnd={e => handleSlotDragEnd(slot.id, e.target.x(), e.target.y())}
            />
          ))}
        </Layer>
      </Stage>
      <SlotPropertiesPanel selectedSlot={selectedSlot} onChange={updateSlot} />
    </div>
  );
}
```

---

### 16.4 A* Pathfinding Service (Python)

The A* implementation lives in the AI service alongside LangGraph. It is a
**deterministic, non-AI tool** — invoked by ASP.NET Core when a booking is confirmed.

**Location:** `ai/routing/astar.py`

```python
import heapq
from typing import Optional
from dataclasses import dataclass, field

@dataclass(order=True)
class _Node:
    f_score: float
    waypoint_id: str = field(compare=False)

def astar(
    graph: dict[str, dict],   # { id: { x, y, neighbors: [id] } }
    start_id: str,             # entry waypoint ID
    goal_id: str,              # nearest waypoint to the booked slot
) -> Optional[list[str]]:
    """
    Returns an ordered list of waypoint IDs from start to goal,
    or None if no path exists.
    Uses Euclidean distance as the heuristic h(n).
    """
    open_set: list[_Node] = []
    heapq.heappush(open_set, _Node(0.0, start_id))

    came_from: dict[str, str] = {}
    g_score: dict[str, float] = {start_id: 0.0}

    def h(node_id: str) -> float:
        """Euclidean distance heuristic."""
        a, b = graph[node_id], graph[goal_id]
        return ((a['x'] - b['x'])**2 + (a['y'] - b['y'])**2) ** 0.5

    while open_set:
        current = heapq.heappop(open_set).waypoint_id

        if current == goal_id:
            # Reconstruct path
            path = []
            while current in came_from:
                path.append(current)
                current = came_from[current]
            path.append(start_id)
            return list(reversed(path))

        for neighbor_id in graph[current]['neighbors']:
            dx = graph[current]['x'] - graph[neighbor_id]['x']
            dy = graph[current]['y'] - graph[neighbor_id]['y']
            tentative_g = g_score[current] + (dx**2 + dy**2)**0.5

            if tentative_g < g_score.get(neighbor_id, float('inf')):
                came_from[neighbor_id] = current
                g_score[neighbor_id] = tentative_g
                f = tentative_g + h(neighbor_id)
                heapq.heappush(open_set, _Node(f, neighbor_id))

    return None  # No path found — safe failure
```

**ASP.NET Core calls it via HTTP:**
```
POST http://ai:8000/routing/path
Body: { "floorPlanId": "...", "entryWaypointId": "...", "slotId": "..." }

Response:
{
  "path": ["wp-001", "wp-005", "wp-012", "wp-019"],
  "coordinates": [
    { "x": 120, "y": 80 },
    { "x": 240, "y": 80 },
    { "x": 240, "y": 300 },
    { "x": 390, "y": 300 }
  ],
  "distancePx": 530.4
}
```

---

### 16.5 Flutter — Indoor Map Viewer

The driver sees the indoor map after a booking is confirmed. The route is drawn
on top of the floor-plan blueprint image using Flutter's `CustomPaint`.

**Libraries:**
- `flutter` built-ins only: `InteractiveViewer`, `Stack`, `Positioned`, `CustomPaint`
- `cached_network_image` — cache the blueprint image from R2

```dart
// mobile/lib/modules/space_availability/indoor_map_screen.dart

class IndoorMapScreen extends ConsumerWidget {
  const IndoorMapScreen({super.key, required this.bookingId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mapData = ref.watch(indoorMapProvider(bookingId));

    return mapData.when(
      loading: () => const LoadingIndicator(),
      error: (e, _) => ErrorView(message: e.toString()),
      data: (data) => Scaffold(
        appBar: AppBar(title: Text('Navigate to ${data.slotLabel}')),
        body: InteractiveViewer(           // pinch-to-zoom and pan for free
          minScale: 0.5,
          maxScale: 4.0,
          child: Stack(
            children: [
              // 1. Blueprint background image
              CachedNetworkImage(imageUrl: data.floorPlan.imageUrl),

              // 2. Route line drawn with CustomPaint
              CustomPaint(
                painter: RoutePainter(coordinates: data.routeCoordinates),
              ),

              // 3. All slot boxes as colored overlays
              ...data.slots.map((slot) => Positioned(
                left: slot.canvasX,
                top: slot.canvasY,
                width: slot.canvasW,
                height: slot.canvasH,
                child: SlotBox(slot: slot),
              )),

              // 4. Pulsing marker on the target slot
              Positioned(
                left: data.targetSlot.canvasX + data.targetSlot.canvasW / 2,
                top:  data.targetSlot.canvasY + data.targetSlot.canvasH / 2,
                child: const PulsingMarker(),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Draws the A* route as a thick blue polyline with an animated dash
class RoutePainter extends CustomPainter {
  final List<Offset> coordinates;
  RoutePainter({required this.coordinates});

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = const Color(0xFF3B82F6)   // Tailwind blue-500
      ..strokeWidth = 4.0
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..style = PaintingStyle.stroke;

    final path = Path();
    if (coordinates.isEmpty) return;
    path.moveTo(coordinates.first.dx, coordinates.first.dy);
    for (final point in coordinates.skip(1)) {
      path.lineTo(point.dx, point.dy);
    }
    canvas.drawPath(path, paint);

    // Draw an arrow at the end
    _drawArrow(canvas, coordinates, paint);
  }

  void _drawArrow(Canvas canvas, List<Offset> coords, Paint paint) {
    if (coords.length < 2) return;
    final last  = coords.last;
    final prev  = coords[coords.length - 2];
    final angle = (last - prev).direction;
    // Draw triangle pointing in direction of travel
    final arrowPath = Path()
      ..moveTo(last.dx, last.dy)
      ..lineTo(last.dx - 12 * cos(angle - 0.4), last.dy - 12 * sin(angle - 0.4))
      ..lineTo(last.dx - 12 * cos(angle + 0.4), last.dy - 12 * sin(angle + 0.4))
      ..close();
    canvas.drawPath(arrowPath, paint..style = PaintingStyle.fill);
  }

  @override
  bool shouldRepaint(RoutePainter old) => old.coordinates != coordinates;
}
```

---

### 16.6 API Endpoints (Space & Availability Component)

All endpoints are behind JWT auth. Mutation endpoints require `ParkingAdmin` or
`SystemAdmin` role.

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/zones/{zoneId}/floor-plans` | List all floors for a zone |
| `POST` | `/api/zones/{zoneId}/floor-plans` | Create new floor (upload image to R2, save URL) |
| `PUT` | `/api/floor-plans/{id}` | Save full slot layout + waypoint graph (admin editor save) |
| `GET` | `/api/floor-plans/{id}` | Get floor plan with all slots (live occupancy included) |
| `DELETE` | `/api/floor-plans/{id}` | Delete floor plan |
| `POST` | `/api/floor-plans/{id}/upload-image` | Get R2 presigned upload URL for blueprint image |
| `GET` | `/api/bookings/{bookingId}/route` | ASP.NET Core calls A* service, returns route coordinates to Flutter |

---

### 16.7 End-to-End Booking + Navigation Flow

```
Driver (Flutter)                ASP.NET Core             Python AI service      PostgreSQL (Neon)
      │                               │                          │                      │
      │── Search for lot ────────────►│                          │                      │
      │◄─ Returns lots + occupancy ───│◄──────────────────────── │ ─────────────────────│
      │                               │                          │                      │
      │── Tap lot → View indoor map ─►│                          │                      │
      │◄─ FloorPlan JSON + Slot data ─│                          │                      │
      │   (renders blueprint + boxes) │                          │                      │
      │                               │                          │                      │
      │── Confirm Booking ───────────►│─── INSERT booking ──────────────────────────────►│
      │                               │                          │                      │
      │                               │─── POST /routing/path ──►│                      │
      │                               │                          │── A* runs on graph   │
      │                               │◄── Route coordinates ────│                      │
      │                               │                          │                      │
      │◄─ Booking confirmed + Route ──│                          │                      │
      │   (draws blue path on map)    │                          │                      │
      │                               │                          │                      │
      │── Scan QR at entrance ───────►│─── Activate session ────────────────────────────►│
      │◄─ Gate opens / Session live ──│                          │                      │
```

---

### 16.8 Component Ownership Update

This feature is fully owned by **Student 2 (Space & Availability)**:

| Layer | Work Item |
|---|---|
| Backend | `FloorPlansController`, `FloorPlanService`, `RoutingService` (calls A* endpoint) |
| Database | `floor_plans` table + migrations + `parking_slots` canvas columns |
| React | `FloorPlanEditor` (React-Konva), `FloorPlanList`, `OccupancyOverlay` |
| Flutter | `IndoorMapScreen`, `RoutePainter`, `SlotBox`, `PulsingMarker`, `IndoorMapProvider` |
| AI service | `ai/routing/astar.py` + FastAPI route `POST /routing/path` |

This counts as Student 2's **distinct Agentic AI contribution** (the Analyzer Agent)
and also their **meaningful device feature** contribution alongside the Flutter QR scanner.
The A* service is a rule-based deterministic component — exactly what the spec means
by "deterministic validation" — and it integrates through ASP.NET Core as required.

---

## 17. Real-Time Occupancy Updates (SignalR)

**Owner:** Student 2 — Space & Availability Component

Without live updates, the indoor map and lot listing show stale data. When a QR scan
activates or closes a session, every connected client must see the slot colour change
instantly. ASP.NET Core's built-in **SignalR** solves this without any extra services.

### 17.1 How It Works

```
QR Scan (Flutter) → POST /api/sessions/start
        │
        ▼
ASP.NET Core: UPDATE parking_slots SET is_occupied = true WHERE id = @slotId
        │
        ▼
SlotHub.Clients.Group("floor-{floorPlanId}")
        .SendAsync("SlotUpdated", { slotId, isOccupied: true })
        │
        ├──► React admin dashboard  (slot turns red on the floor plan viewer)
        └──► All Flutter clients viewing that floor (slot turns red instantly)
```

### 17.2 ASP.NET Core Hub

```csharp
// api/src/Modules/SpaceAvailability/Hubs/SlotHub.cs
[Authorize]
public class SlotHub : Hub
{
    // Client calls this after loading a floor plan to receive updates for it
    public async Task SubscribeToFloor(string floorPlanId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"floor-{floorPlanId}");

    public async Task UnsubscribeFromFloor(string floorPlanId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"floor-{floorPlanId}");
}
```

```csharp
// Called inside SessionService after INSERT/UPDATE
public class SlotOccupancyNotifier(IHubContext<SlotHub> hub)
{
    public async Task NotifySlotChanged(string floorPlanId, string slotId, bool isOccupied)
        => await hub.Clients
               .Group($"floor-{floorPlanId}")
               .SendAsync("SlotUpdated", new { slotId, isOccupied });
}
```

Map in `Program.cs`:
```csharp
app.MapHub<SlotHub>("/hubs/slots");
```

### 17.3 React — useSignalR Hook

```tsx
// web/src/hooks/useSlotUpdates.ts
export function useSlotUpdates(floorPlanId: string) {
  const updateSlot = useSpaceStore(s => s.updateSlotOccupancy);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/slots', { accessTokenFactory: () => getToken() })
      .withAutomaticReconnect()
      .build();

    connection.on('SlotUpdated', ({ slotId, isOccupied }) => {
      updateSlot(slotId, isOccupied);   // Zustand store triggers re-render
    });

    connection.start()
      .then(() => connection.invoke('SubscribeToFloor', floorPlanId));

    return () => { connection.stop(); };
  }, [floorPlanId]);
}
```

### 17.4 Flutter — SignalR Client

```dart
// mobile/lib/modules/space_availability/providers/slot_hub_provider.dart
// Uses: signalr_netcore package

final slotHubProvider = Provider.autoDispose.family<SlotHubService, String>(
  (ref, floorPlanId) {
    final service = SlotHubService(floorPlanId: floorPlanId);
    service.connect();
    ref.onDispose(service.disconnect);
    return service;
  },
);

class SlotHubService {
  final HubConnection _connection;

  Future<void> connect() async {
    _connection = HubConnectionBuilder()
      .withUrl('$baseUrl/hubs/slots',
               options: HttpConnectionOptions(accessTokenProvider: getToken))
      .withAutomaticReconnect()
      .build();

    _connection.on('SlotUpdated', (args) {
      // Notify Riverpod provider → IndoorMapScreen re-renders slot colour
    });

    await _connection.start();
    await _connection.invoke('SubscribeToFloor', args: [floorPlanId]);
  }
}
```

### 17.5 Database Change

No new table needed. Add a computed view for the React lot list:

```sql
CREATE VIEW zone_occupancy AS
SELECT
  z.id          AS zone_id,
  z.name,
  COUNT(ps.id)                              AS total_slots,
  COUNT(ps.id) FILTER (WHERE ps.is_occupied) AS occupied_slots,
  ROUND(
    COUNT(ps.id) FILTER (WHERE ps.is_occupied)::NUMERIC /
    NULLIF(COUNT(ps.id), 0) * 100, 1
  )                                         AS occupancy_pct
FROM zones z
JOIN parking_slots ps ON ps.zone_id = z.id
GROUP BY z.id, z.name;
```

---

## 18. Disabled Parking Permit Verification

**Owner:** Student 1 — User & Access Component

"Disabled parking verification" is stated in the system overview and maps directly to
the **Validator Agent's** core responsibility. This section fully designs the end-to-end
flow from permit upload in Flutter to AI validation and human approval in React.

### 18.1 Database Schema

```sql
CREATE TABLE disability_permits (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  permit_number   VARCHAR(100),                -- extracted by OCR
  issuing_body    VARCHAR(200),
  expiry_date     DATE,
  image_url       TEXT NOT NULL,               -- Cloudflare R2 (private bucket)
  status          VARCHAR(30) NOT NULL DEFAULT 'PENDING',
  -- PENDING | VERIFIED | REJECTED | EXPIRED
  rejection_reason TEXT,
  verified_by     UUID REFERENCES users(id),   -- admin who approved
  verified_at     TIMESTAMPTZ,
  ocr_raw_result  JSONB,                       -- raw Cloudflare AI Vision output
  created_at      TIMESTAMPTZ DEFAULT NOW(),
  updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Drivers with VERIFIED permits can book disabled slots
ALTER TABLE users ADD COLUMN has_disability_permit BOOLEAN DEFAULT FALSE;
```

### 18.2 Flutter — Permit Upload Flow

```dart
// mobile/lib/modules/user_access/permit_upload_screen.dart
// Device feature: camera OR file picker (satisfies spec requirement)

class PermitUploadScreen extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: const Text('Upload Disability Permit')),
      body: Column(children: [
        // Show current status badge
        PermitStatusBadge(status: ref.watch(permitStatusProvider)),

        ElevatedButton.icon(
          icon: const Icon(Icons.camera_alt),
          label: const Text('Take Photo'),
          onPressed: () => _pickAndUpload(ImageSource.camera, ref),
        ),
        ElevatedButton.icon(
          icon: const Icon(Icons.upload_file),
          label: const Text('Upload from Files'),
          onPressed: () => _pickAndUpload(ImageSource.gallery, ref),
        ),
      ]),
    );
  }

  Future<void> _pickAndUpload(ImageSource source, WidgetRef ref) async {
    final picked = await ImagePicker().pickImage(source: source, imageQuality: 85);
    if (picked == null) return;
    // POST /api/permits → receives R2 presigned URL → uploads directly
    // → triggers Validator Agent workflow
    await ref.read(permitUploadNotifierProvider.notifier).upload(File(picked.path));
  }
}
```

### 18.3 Validator Agent — Permit Verification Workflow

This is the **primary workflow** for the Validator Agent (Student 1's AI contribution).
It is triggered by `POST /api/permits` and runs as a LangGraph workflow.

```
[TRIGGER: Permit uploaded]
        │
        ▼
[VALIDATOR AGENT]
  Tool 1: ocr_extract_permit_data
    → Calls Cloudflare Workers AI (vision model)
    → Extracts: permit_number, issuing_body, expiry_date
    → Returns structured JSON or error
        │
        ▼
  Tool 2: validate_permit_schema
    → Checks: all required fields present?
    → Checks: expiry_date > today?
    → Checks: permit_number matches known format (regex)
        │
  Pass ─┴─ Fail
    │           │
    ▼           ▼
[PAUSE:    [AUTO-REJECT]
 Human     status = REJECTED
 Approval] reason = "Expired" / "Invalid format"
    │
  Admin reviews image + OCR result in React
  Clicks Approve → status = VERIFIED, user.has_disability_permit = true
  Clicks Reject  → status = REJECTED, reason saved, Resend email sent to driver
```

### 18.4 ASP.NET Core — Permit Endpoints

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/permits` | Upload permit image → get R2 URL → trigger Validator Agent |
| `GET` | `/api/permits/me` | Driver: get own permit status |
| `GET` | `/api/permits/pending` | Admin: list all permits awaiting review |
| `POST` | `/api/permits/{id}/approve` | Admin: approve (sets user.has_disability_permit = true) |
| `POST` | `/api/permits/{id}/reject` | Admin: reject with reason |
| `GET` | `/api/permits/{id}/image` | Get presigned R2 URL for permit image (admin only) |

### 18.5 React — Permit Review Panel

The React admin dashboard shows a queue of pending permits:

```
┌─────────────────────────────────────────────────────────────┐
│  Disability Permit Review                    3 pending       │
├──────────┬──────────────┬───────────┬────────┬─────────────┤
│ Driver   │ Permit No.   │ Expiry    │ Status │ Actions      │
├──────────┼──────────────┼───────────┼────────┼─────────────┤
│ John D.  │ DIS-2024-991 │ 2027-06   │ ⏳ AI  │ [View Image] │
│          │              │           │ Done   │ [Approve]    │
│          │ OCR result ↓ │           │        │ [Reject]     │
│          │ ✓ Valid fmt  │           │        │              │
│          │ ✓ Not expired│           │        │              │
└──────────┴──────────────┴───────────┴────────┴─────────────┘
```

---

## 19. Parking Session Lifecycle & Fee Calculation

**Owner:** Student 3 — Booking & Payment Component

This is the core business logic of the entire system. A **session** is the period
between a driver scanning in and scanning out. Fees are calculated from duration,
zone base rate, and any active AI-generated surge multiplier.

### 19.1 Database Schema

```sql
CREATE TABLE parking_sessions (
  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  booking_id        UUID NOT NULL REFERENCES bookings(id),
  slot_id           UUID NOT NULL REFERENCES parking_slots(id),
  user_id           UUID NOT NULL REFERENCES users(id),
  -- QR scan timestamps
  checked_in_at     TIMESTAMPTZ,
  checked_out_at    TIMESTAMPTZ,
  -- Duration
  duration_minutes  INT GENERATED ALWAYS AS (
    EXTRACT(EPOCH FROM (checked_out_at - checked_in_at)) / 60
  ) STORED,
  -- Fee breakdown (immutable snapshot at checkout time)
  base_rate_per_hour  NUMERIC(10,2) NOT NULL,
  surge_multiplier    NUMERIC(4,2) NOT NULL DEFAULT 1.00,
  duration_hours      NUMERIC(8,4),
  subtotal            NUMERIC(10,2),
  overstay_penalty    NUMERIC(10,2) NOT NULL DEFAULT 0,
  discount_amount     NUMERIC(10,2) NOT NULL DEFAULT 0,  -- disability discount etc.
  total_fee           NUMERIC(10,2),
  -- Status
  status  VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',
  -- ACTIVE | COMPLETED | OVERSTAY | DISPUTED
  receipt_url   TEXT,     -- R2 PDF receipt link
  created_at    TIMESTAMPTZ DEFAULT NOW(),
  updated_at    TIMESTAMPTZ DEFAULT NOW()
);

-- Active surge multipliers set by AI workflow
CREATE TABLE zone_pricing_rules (
  id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  zone_id          UUID NOT NULL REFERENCES zones(id),
  multiplier       NUMERIC(4,2) NOT NULL DEFAULT 1.00,
  reason           TEXT,                  -- AI justification summary
  approved_by      UUID REFERENCES users(id),
  active_from      TIMESTAMPTZ NOT NULL,
  active_until     TIMESTAMPTZ,           -- NULL = indefinite
  workflow_run_id  UUID REFERENCES agent_workflow_runs(id),
  created_at       TIMESTAMPTZ DEFAULT NOW()
);
```

### 19.2 Fee Calculation Algorithm

```csharp
// api/src/Modules/Booking/Services/FeeCalculationService.cs
public class FeeCalculationService(IZonePricingRepository pricing)
{
    public SessionFee Calculate(ParkingSession session, Zone zone)
    {
        // 1. Duration (bill in 15-minute increments, minimum 1 block)
        var rawMinutes  = (session.CheckedOutAt - session.CheckedInAt).TotalMinutes;
        var billableMin = Math.Max(15, Math.Ceiling(rawMinutes / 15) * 15);
        var hours       = (decimal)billableMin / 60;

        // 2. Surge multiplier (from latest approved AI pricing rule for this zone)
        var activeRule   = pricing.GetActiveRule(zone.Id, session.CheckedOutAt);
        var multiplier   = activeRule?.Multiplier ?? 1.00m;

        // 3. Subtotal
        var subtotal = zone.HourlyRate * hours * multiplier;

        // 4. Disability discount (15% if user has verified permit)
        var discount = session.User.HasDisabilityPermit ? subtotal * 0.15m : 0m;

        // 5. Overstay penalty (fetched from enforcement table if session is OVERSTAY)
        var penalty = pricing.GetOverstayPenalty(session.Id);

        return new SessionFee(
            BaseRatePerHour : zone.HourlyRate,
            SurgeMultiplier : multiplier,
            DurationHours   : hours,
            Subtotal        : subtotal,
            Discount        : discount,
            OverstayPenalty : penalty,
            Total           : subtotal - discount + penalty
        );
    }
}
```

### 19.3 Session Endpoints

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/sessions/check-in` | QR scan in — validates booking, creates session, notifies SignalR |
| `POST` | `/api/sessions/check-out` | QR scan out — closes session, calculates fee, generates receipt |
| `GET` | `/api/sessions/{id}` | Get full session detail with fee breakdown |
| `GET` | `/api/sessions/active` | Driver: get current active session |
| `GET` | `/api/sessions/history` | Driver: paginated session history |
| `GET` | `/api/sessions/{id}/receipt` | Download PDF receipt (R2 pre-signed URL) |

### 19.4 Flutter — Active Session Screen

After check-in, the Flutter app shows a live session tracker:

```
┌──────────────────────────────────────────┐
│  🅿  Active Session                       │
│                                          │
│  Zone A  ·  Slot B14  ·  Ground Floor    │
│                                          │
│  ⏱  Duration           0h 47m           │
│  💰  Current Estimate  LKR 235.00        │
│                                          │
│  Checked in at         14:23             │
│  Base rate             LKR 300/hr        │
│  Surge                 ×1.2  (Peak)      │
│                                          │
│  ┌──────────────────────────────────┐   │
│  │     📷  Scan to Check Out        │   │
│  └──────────────────────────────────┘   │
└──────────────────────────────────────────┘
```

Fee estimate updates every 60 seconds via polling `GET /api/sessions/active`.

---

## 20. Push Notifications (Firebase Cloud Messaging)

**Owner:** Student 3 — Booking & Payment Component

Push notifications close the loop on the human-in-the-loop AI approval workflow.
When an admin approves or rejects a penalty, the driver's phone buzzes — without
polling. FCM is free, reliable, and works on both Android and iOS.

### 20.1 Architecture

```
Admin clicks "Approve" on React dashboard
        │
        ▼
POST /api/agent/workflows/{id}/approve
        │
        ▼
ASP.NET Core finalises penalty in PostgreSQL
        │
        ▼
NotificationService.SendAsync(userId, title, body)
        │
        ▼
FCM HTTP v1 API  →  Firebase  →  Driver's phone
```

### 20.2 Database

```sql
ALTER TABLE users ADD COLUMN fcm_token TEXT;
ALTER TABLE users ADD COLUMN fcm_token_updated_at TIMESTAMPTZ;
```

### 20.3 ASP.NET Core — NotificationService

```csharp
// api/src/Core/Services/NotificationService.cs
public class NotificationService(IConfiguration config, IUserRepository users)
{
    private readonly string _fcmKey = config["FCM_SERVER_KEY"];

    public async Task SendAsync(Guid userId, string title, string body,
                                 Dictionary<string, string>? data = null)
    {
        var token = await users.GetFcmTokenAsync(userId);
        if (token is null) return;   // User hasn't granted notification permission

        var message = new
        {
            message = new
            {
                token,
                notification = new { title, body },
                data = data ?? new Dictionary<string, string>()
            }
        };

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());

        await http.PostAsJsonAsync(
            $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send",
            message);
    }
}
```

### 20.4 Notification Triggers

| Event | Title | Body |
|---|---|---|
| Booking confirmed | "Booking Confirmed 🅿" | "Slot B14, Zone A — 24 Sep at 14:00" |
| Session checked out | "Receipt Ready 🧾" | "Session complete. Total: LKR 480. Tap to view." |
| Penalty approved | "Penalty Notice ⚠️" | "Overstay fine of LKR 500 has been issued for slot B14." |
| Surge price activated | "Parking Alert 📢" | "Zone A surge pricing is now active (×1.5). Book early." |
| Permit approved | "Permit Verified ✅" | "Your disability permit has been verified. Disabled slots unlocked." |
| Permit rejected | "Permit Update ❌" | "Your disability permit could not be verified. Tap to resubmit." |

### 20.5 Flutter — FCM Setup

```dart
// mobile/lib/core/services/notification_service.dart
class NotificationService {
  static Future<void> init() async {
    await Firebase.initializeApp();
    final messaging = FirebaseMessaging.instance;

    // Request permission (iOS + Android 13+)
    await messaging.requestPermission();

    // Get token and register with backend
    final token = await messaging.getToken();
    if (token != null) {
      await api.patch('/api/users/me/fcm-token', body: { 'token': token });
    }

    // Refresh token when it changes
    messaging.onTokenRefresh.listen((newToken) {
      api.patch('/api/users/me/fcm-token', body: { 'token': newToken });
    });

    // Handle foreground messages → show in-app banner
    FirebaseMessaging.onMessage.listen((msg) {
      showInAppNotificationBanner(msg.notification?.title, msg.notification?.body);
    });
  }
}
```

### 20.6 FCM Secrets

Add to GitHub Org secrets:
- `FCM_PROJECT_ID` — Firebase project ID
- `FCM_SERVICE_ACCOUNT_JSON` — Service account key for FCM HTTP v1 API auth

Add to `.env.example`:
```
FCM_PROJECT_ID=your-firebase-project-id
FCM_SERVICE_ACCOUNT_JSON={"type":"service_account",...}
```

---

## 21. Admin Analytics Dashboard

**Owner:** Student 4 — Enforcement & AI Orchestration Component
*(analytics data lives in enforcement/session tables Student 4 also manages)*

The React admin dashboard requires "dashboard views" and "reporting or analytics"
per the assignment spec. These are React-only views backed by read-only
aggregate API endpoints.

### 21.1 Dashboard Layout

```
┌─────────────────────────────────────────────────────────────────┐
│  OpenParking Admin — Overview                   📅 Last 30 days  │
├──────────┬──────────────┬──────────────┬────────────────────────┤
│ 🅿 Total │ 💰 Revenue   │ ⏱ Avg Stay  │ ⚠️ Violations          │
│ 1,240    │ LKR 186,500  │ 1h 42m      │ 47 overstays           │
│ sessions │ this month   │             │ 12 pending review      │
├──────────┴──────────────┴──────────────┴────────────────────────┤
│  Revenue (daily)           │  Occupancy by Zone (live)          │
│  [Line chart — 30 days]    │  Zone A ████████░░ 78%             │
│                            │  Zone B ██████░░░░ 58%             │
│                            │  Zone C ████░░░░░░ 41%             │
├────────────────────────────┴───────────────────────────────────┤
│  Violations Trend (weekly bar chart)                            │
│  [Bar chart: overstay count per week]                           │
├────────────────────────────────────────────────────────────────┤
│  AI Workflow Activity                                           │
│  12 completed · 3 awaiting approval · 1 failed (safe)          │
└────────────────────────────────────────────────────────────────┘
```

### 21.2 Analytics API Endpoints

| Method | Route | Returns |
|---|---|---|
| `GET` | `/api/analytics/summary` | Total sessions, revenue, avg duration, violation count for period |
| `GET` | `/api/analytics/revenue/daily` | `[{ date, revenue }]` — last N days |
| `GET` | `/api/analytics/occupancy` | Live occupancy % per zone (uses `zone_occupancy` view from §17) |
| `GET` | `/api/analytics/violations/weekly` | `[{ week, count }]` — overstay count per week |
| `GET` | `/api/analytics/workflows/summary` | AI workflow status counts |

All accept `?from=&to=` date range query params and require `ParkingAdmin` or `SystemAdmin` role.

### 21.3 React — Chart Components

```tsx
// web/src/modules/enforcement/AnalyticsDashboard.tsx
// Uses: recharts (free, lightweight, ~150KB)

import { LineChart, BarChart, Line, Bar, XAxis, YAxis,
         CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';

export function RevenueTrendChart() {
  const { data } = useAnalyticsQuery('revenue/daily', { days: 30 });

  return (
    <ResponsiveContainer width="100%" height={280}>
      <LineChart data={data}>
        <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
        <XAxis dataKey="date" tick={{ fill: '#94a3b8', fontSize: 12 }} />
        <YAxis tick={{ fill: '#94a3b8', fontSize: 12 }} />
        <Tooltip contentStyle={{ background: '#0f172a', border: 'none' }} />
        <Line type="monotone" dataKey="revenue"
              stroke="#3b82f6" strokeWidth={2} dot={false} />
      </LineChart>
    </ResponsiveContainer>
  );
}
```

**Library:** `recharts` — add to `web/package.json`.
No GIS or heavy mapping library needed for analytics.

---

## 22. Search, Filtering, Sorting & Pagination

**Owner:** All components — each student implements for their own endpoints

The assignment spec explicitly requires search, filtering, sorting and pagination across
both React and Flutter. This section defines the **shared API contract** so all four
students implement it consistently.

### 22.1 Standard Query Parameters (all list endpoints)

| Param | Type | Example | Description |
|---|---|---|---|
| `search` | string | `?search=Zone+A` | Full-text search on relevant text fields |
| `page` | int | `?page=2` | 1-indexed page number (default: 1) |
| `pageSize` | int | `?pageSize=20` | Items per page (default: 20, max: 100) |
| `sortBy` | string | `?sortBy=createdAt` | Field to sort on |
| `sortDir` | `asc\|desc` | `?sortDir=desc` | Sort direction (default: desc) |

Each component adds its own **filter params** on top:

| Component | Filter Params |
|---|---|
| Space & Availability | `?zoneId=&slotType=&isOccupied=&minPrice=&maxPrice=` |
| Booking & Payment | `?status=&userId=&slotId=&from=&to=` |
| Enforcement | `?status=&severity=&from=&to=&userId=` |
| User & Access | `?role=&permitStatus=&isActive=` |

### 22.2 Standard Paginated Response Envelope

Every list endpoint returns this JSON shape — consistent across all four components:

```json
{
  "data": [ ... ],
  "pagination": {
    "page": 2,
    "pageSize": 20,
    "totalItems": 154,
    "totalPages": 8,
    "hasNextPage": true,
    "hasPreviousPage": true
  }
}
```

### 22.3 ASP.NET Core — Shared PaginatedQuery Base

```csharp
// api/src/Core/Models/PaginatedQuery.cs
public record PaginatedQuery
{
    public string?  Search   { get; init; }
    public int      Page     { get; init; } = 1;
    public int      PageSize { get; init; } = 20;
    public string   SortBy   { get; init; } = "createdAt";
    public string   SortDir  { get; init; } = "desc";

    public int Skip => (Page - 1) * Math.Clamp(PageSize, 1, 100);
    public int Take => Math.Clamp(PageSize, 1, 100);
}

// api/src/Core/Models/PagedResult.cs
public record PagedResult<T>(
    IReadOnlyList<T> Data,
    int Page, int PageSize, long TotalItems)
{
    public int  TotalPages      => (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasNextPage     => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

### 22.4 EF Core — Generic Extension

```csharp
// api/src/Core/Extensions/QueryableExtensions.cs
public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PaginatedQuery q,
        CancellationToken ct = default)
    {
        var total = await query.LongCountAsync(ct);
        var data  = await query
            .Skip(q.Skip)
            .Take(q.Take)
            .ToListAsync(ct);

        return new PagedResult<T>(data, q.Page, q.Take, total);
    }
}
```

Usage in any controller: `await _db.Bookings.Where(...).ToPagedResultAsync(query);`

### 22.5 React — Shared usePaginatedQuery Hook

```tsx
// web/src/hooks/usePaginatedQuery.ts
export function usePaginatedQuery<T>(endpoint: string, filters: object = {}) {
  const [page, setPage]     = useState(1);
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState('createdAt');
  const [sortDir, setSortDir] = useState<'asc'|'desc'>('desc');

  const query = useQuery({
    queryKey: [endpoint, { page, search, sortBy, sortDir, ...filters }],
    queryFn: () => api.get<PagedResponse<T>>(endpoint, {
      params: { page, pageSize: 20, search, sortBy, sortDir, ...filters }
    }),
    placeholderData: keepPreviousData,   // no flicker between pages
  });

  return { ...query, page, setPage, search, setSearch, setSortBy, setSortDir };
}
```

### 22.6 Flutter — Infinite Scroll (Lot List)

```dart
// Uses: infinite_scroll_pagination package

class LotSearchScreen extends ConsumerStatefulWidget { ... }

class _LotSearchScreenState extends ConsumerState<LotSearchScreen> {
  final _pagingController = PagingController<int, ParkingLot>(firstPageKey: 1);
  String _search = '';

  @override
  void initState() {
    super.initState();
    _pagingController.addPageRequestListener(_fetchPage);
  }

  Future<void> _fetchPage(int pageKey) async {
    try {
      final result = await ref.read(lotRepositoryProvider).search(
        search: _search, page: pageKey, pageSize: 20,
      );
      final isLast = pageKey >= result.pagination.totalPages;
      isLast
        ? _pagingController.appendLastPage(result.data)
        : _pagingController.appendPage(result.data, pageKey + 1);
    } catch (e) {
      _pagingController.error = e;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(children: [
      SearchBar(onChanged: (v) {
        _search = v;
        _pagingController.refresh();   // restart pagination on new search
      }),
      Expanded(
        child: PagedListView<int, ParkingLot>(
          pagingController: _pagingController,
          builderDelegate: PagedChildBuilderDelegate<ParkingLot>(
            itemBuilder: (ctx, lot, _) => LotCard(lot: lot),
            firstPageProgressIndicatorBuilder: (_) => const LoadingIndicator(),
            noItemsFoundIndicatorBuilder:      (_) => const EmptyState(),
            firstPageErrorIndicatorBuilder:    (ctx) => ErrorView(
              onRetry: _pagingController.refresh,
            ),
          ),
        ),
      ),
    ]);
  }
}
```

---

## 23. Gap Resolution Summary

All previously identified gaps are now fully designed:

| # | Gap | Section | Owner | Status |
|---|---|---|---|---|
| 1 | Real-time occupancy | §17 SignalR | Student 2 | ✅ Designed |
| 2 | Disabled permit verification | §18 Validator Agent | Student 1 | ✅ Designed |
| 3 | Session lifecycle + fee calculation | §19 | Student 3 | ✅ Designed |
| 4 | Push notifications (FCM) | §20 | Student 3 | ✅ Designed |
| 5 | Admin analytics dashboard | §21 | Student 4 | ✅ Designed |
| 6 | Search / filter / sort / pagination | §22 | All | ✅ Designed |
| ADR-009 | Configuration storage | **`system_settings` DB table + IMemoryCache** | Zero hardcoded business values; admin-editable at runtime; cache prevents N+1 DB hits |

---

## 25. Testing Strategy & Observability

**Owner:** Shared Infrastructure / All Components

To achieve maximum marks in the "Testing, CI & Git Workflow" and "API Integration" rubrics, OpenParking enforces a strict testing and error-handling standard across all stacks. This ensures reliability and provides clear evidence of professional software engineering practices.

### 25.1 Comprehensive Testing Strategy

Each stack has a defined testing framework and methodology. CI pipelines automatically block PRs if these tests fail.

| Stack | Framework | Methodology |
|---|---|---|
| **ASP.NET Core** | **xUnit + Moq** | **Unit Tests:** Mock database and external services to test business logic (e.g., `FeeCalculationService`).<br>**Integration Tests:** Use `WebApplicationFactory` with a transient PostgreSQL container (Testcontainers) to test full HTTP request lifecycles. |
| **React Web** | **Vitest + RTL** | **Component Tests:** React Testing Library (RTL) ensures UI components render correctly based on props.<br>**API Mocks:** Use MSW (Mock Service Worker) to intercept requests and return mock JSON during tests. |
| **Flutter Mobile** | **flutter_test** | **Unit Tests:** Test Riverpod providers and local logic.<br>**Widget Tests:** Pump widgets to verify UI interactions.<br>**Mocking:** Use Riverpod's `overrideWithValue` to inject fake repositories during tests. |
| **Python AI** | **pytest** | **Unit/Graph Tests:** Test LangGraph state transitions deterministically.<br>**Mocking:** Use `vcrpy` or mocked HTTP clients to prevent CI from calling real Cloudflare LLM endpoints, keeping tests fast, deterministic, and free. |

### 25.2 Global Error Handling & RFC 7807

To prevent stack traces from leaking and to provide a consistent contract for the frontends, ASP.NET Core implements a Global Exception Handler conforming to **RFC 7807 (Problem Details)**.

```csharp
// api/src/Core/Middleware/GlobalExceptionHandler.cs
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred");

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server Error",
            Detail = "An unexpected error occurred. Please try again later.",
            Instance = httpContext.Request.Path
        };

        if (exception is ValidationException ve)
        {
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Title = "Validation Failed";
            problemDetails.Detail = ve.Message;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
```

### 25.3 Structured Logging (Serilog)

Instead of plain text logs, ASP.NET Core uses **Serilog** to write structured JSON logs.

*   **Correlation IDs:** Every request passing through Cloudflare receives a `CF-Ray` header. ASP.NET Core logs this ID with every message. If a user reports an issue, the admin can trace the exact request through the Edge Gateway, the API, and the AI service.
*   **Docker Integration:** Serilog writes to the console, and Docker Compose captures these logs. `make logs` streams them for local debugging.

### 25.4 Frontend Resilience

*   **React:** Utilizes **Error Boundaries** (`<ErrorBoundary>`) at the route level. If a component crashes, the user sees a styled "Something went wrong" fallback UI instead of a blank white screen.
*   **Flutter:** Implements global error catching via `FlutterError.onError` and `PlatformDispatcher.instance.onError` to gracefully handle Dart exceptions and prevent hard app crashes.

---

The design.md now covers every mandatory and non-functional requirement in the SE3090 Assignment 1 specification, providing a complete blueprint for an A+ grade project.
---

## 24. System Configuration Management

**Owner:** Student 1 — User & Access Component
*(System Admin role owns configuration; settings read by all components)*

No business value should be hardcoded. All operational thresholds, fees, time windows,
and toggle flags live in the database, are editable by the **System Admin** from the
React dashboard, and are cached in ASP.NET Core memory — so a config change takes
effect within seconds without a redeploy.

### 24.1 Complete List of Configurable Values

#### 💰 Pricing & Fees

| Key | Default | Description |
|---|---|---|
| `pricing.base_hourly_rate_default` | `300.00` | Fallback rate (LKR/hr) if zone has no custom rate |
| `pricing.billing_increment_minutes` | `15` | Bill in blocks of N minutes (min 1 block charged) |
| `pricing.minimum_fee` | `75.00` | Minimum charge per session regardless of duration |
| `pricing.disability_discount_pct` | `15` | % discount for drivers with verified disability permit |
| `pricing.currency_code` | `LKR` | ISO 4217 currency code displayed in Flutter/React |
| `pricing.currency_symbol` | `LKR` | Symbol shown in UI |

#### 📈 Surge Pricing

| Key | Default | Description |
|---|---|---|
| `surge.occupancy_trigger_pct` | `80` | Occupancy % at which AI surge workflow fires |
| `surge.max_multiplier_cap` | `3.00` | AI cannot propose surge above this (Validator Agent hard-blocks) |
| `surge.min_multiplier_step` | `0.10` | Smallest increment AI may propose |
| `surge.cooldown_after_rejection_minutes` | `60` | Min time before AI can re-propose surge for same zone |
| `surge.auto_expire_hours` | `4` | Active surge rule expires after N hours if not manually extended |

#### ⚠️ Overstay & Penalties

| Key | Default | Description |
|---|---|---|
| `overstay.grace_period_minutes` | `15` | Minutes past booking end before session becomes OVERSTAY |
| `overstay.penalty_fixed_amount` | `500.00` | Base overstay fine (LKR) |
| `overstay.penalty_per_extra_hour` | `200.00` | Additional fine per extra hour beyond grace period |
| `overstay.max_penalty_cap` | `2000.00` | Maximum total fine AI can propose (Validator Agent enforces) |
| `overstay.escalation_threshold_hours` | `2` | After N hours, fine doubles |
| `overstay.warning_lead_minutes` | `10` | Send "expiring soon" push notification N minutes before booking end |

#### 📅 Booking Rules

| Key | Default | Description |
|---|---|---|
| `booking.max_advance_days` | `30` | How far in advance a driver can book |
| `booking.min_notice_minutes` | `15` | Minimum lead time before booking start |
| `booking.max_active_bookings_per_driver` | `2` | Max concurrent bookings per user |
| `booking.free_cancel_window_minutes` | `60` | Cancel up to N minutes before start for no fee |
| `booking.no_show_release_minutes` | `20` | Auto-release slot if driver hasn't checked in N min after start |
| `booking.max_duration_hours` | `24` | Maximum single booking duration |

#### 🔔 Notifications

| Key | Default | Description |
|---|---|---|
| `notify.session_expiry_reminder_minutes` | `30` | Push notification N min before booking end |
| `notify.permit_review_sla_hours` | `24` | Admin SLA to review permit (reminder email if exceeded) |
| `notify.email_on_booking_confirm` | `true` | Send Resend email on booking confirmation |
| `notify.email_on_penalty_issued` | `true` | Send Resend email when penalty is finalised |
| `notify.push_on_surge_activated` | `true` | Push notification to drivers in affected zone |

#### 🔐 Security

| Key | Default | Description |
|---|---|---|
| `auth.jwt_expiry_hours` | `24` | JWT access token lifetime |
| `auth.refresh_token_expiry_days` | `30` | Refresh token lifetime |
| `auth.max_failed_login_attempts` | `5` | Lockout after N consecutive failures |
| `auth.lockout_duration_minutes` | `15` | Duration of account lockout |

#### 🖥️ System

| Key | Default | Description |
|---|---|---|
| `system.r2_presigned_url_expiry_seconds` | `3600` | R2 presigned URL TTL |
| `system.rate_limit_per_ip_per_minute` | `100` | CF Worker rate limit (also enforced in ASP.NET) |
| `system.session_estimate_poll_seconds` | `60` | Flutter polls active session estimate every N seconds |
| `system.settings_cache_ttl_seconds` | `30` | How often the in-memory settings cache refreshes |
| `system.maintenance_mode` | `false` | Toggle: API returns 503, Flutter shows maintenance screen |
| `system.allow_new_registrations` | `true` | Toggle: disable sign-ups without deleting accounts |

---

### 24.2 Database Schema

```sql
CREATE TABLE system_settings (
  key          VARCHAR(100) PRIMARY KEY,
  value        TEXT NOT NULL,
  data_type    VARCHAR(20) NOT NULL,    -- 'string' | 'integer' | 'decimal' | 'boolean'
  category     VARCHAR(50) NOT NULL,    -- 'pricing' | 'surge' | 'overstay' | 'booking'
                                        --  | 'notify' | 'auth' | 'system'
  label        VARCHAR(200) NOT NULL,   -- Human-readable label shown in React UI
  description  TEXT,                    -- Tooltip in React UI
  min_value    TEXT,                    -- Validation: minimum (for numbers)
  max_value    TEXT,                    -- Validation: maximum (for numbers)
  requires_restart BOOLEAN DEFAULT FALSE,
  updated_by   UUID REFERENCES users(id),
  updated_at   TIMESTAMPTZ DEFAULT NOW()
);

-- Per-zone overrides for pricing (overrides system-wide defaults)
CREATE TABLE zone_pricing_config (
  zone_id              UUID PRIMARY KEY REFERENCES zones(id) ON DELETE CASCADE,
  hourly_rate          NUMERIC(10,2),       -- NULL = use system default
  disability_discount_pct NUMERIC(5,2),     -- NULL = use system default
  max_duration_hours   INT,                 -- NULL = use system default
  updated_by           UUID REFERENCES users(id),
  updated_at           TIMESTAMPTZ DEFAULT NOW()
);
```

---

### 24.3 ASP.NET Core — ISettingsService (Cached)

Settings are read-through cached. Every call reads from an in-memory `IMemoryCache`.
The cache is invalidated on any write to `system_settings`.

```csharp
// api/src/Core/Services/ISettingsService.cs
public interface ISettingsService
{
    Task<decimal>  GetDecimalAsync(string key);
    Task<int>      GetIntAsync(string key);
    Task<bool>     GetBoolAsync(string key);
    Task<string>   GetStringAsync(string key);
    Task           SetAsync(string key, string value, Guid updatedBy);
    Task           InvalidateCacheAsync();
}

// api/src/Core/Services/SettingsService.cs
public class SettingsService(AppDbContext db, IMemoryCache cache) : ISettingsService
{
    private const string CachePrefix = "setting:";

    public async Task<decimal> GetDecimalAsync(string key)
    {
        var raw = await GetRawAsync(key);
        return decimal.Parse(raw, CultureInfo.InvariantCulture);
    }

    public async Task<int> GetIntAsync(string key)
        => int.Parse(await GetRawAsync(key));

    public async Task<bool> GetBoolAsync(string key)
        => bool.Parse(await GetRawAsync(key));

    public async Task<string> GetStringAsync(string key)
        => await GetRawAsync(key);

    private async Task<string> GetRawAsync(string key)
    {
        var cacheKey = CachePrefix + key;
        if (cache.TryGetValue(cacheKey, out string? cached)) return cached!;

        var setting = await db.SystemSettings.FindAsync(key)
            ?? throw new KeyNotFoundException($"Setting '{key}' not found.");

        // Cache TTL comes from the setting itself (self-referential!)
        var ttl = int.Parse(
            (await db.SystemSettings.FindAsync("system.settings_cache_ttl_seconds"))?.Value ?? "30"
        );

        cache.Set(cacheKey, setting.Value, TimeSpan.FromSeconds(ttl));
        return setting.Value;
    }

    public async Task SetAsync(string key, string value, Guid updatedBy)
    {
        var setting = await db.SystemSettings.FindAsync(key)
            ?? throw new KeyNotFoundException($"Setting '{key}' not found.");

        // Type validation before saving
        ValidateType(setting.DataType, value, setting.MinValue, setting.MaxValue);

        setting.Value     = value;
        setting.UpdatedBy = updatedBy;
        setting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Invalidate this key's cache entry
        cache.Remove(CachePrefix + key);
    }

    private static void ValidateType(string type, string value, string? min, string? max)
    {
        switch (type)
        {
            case "decimal":
                var d = decimal.Parse(value, CultureInfo.InvariantCulture);
                if (min != null && d < decimal.Parse(min)) throw new ArgumentException("Below minimum.");
                if (max != null && d > decimal.Parse(max)) throw new ArgumentException("Above maximum.");
                break;
            case "integer":
                var i = int.Parse(value);
                if (min != null && i < int.Parse(min)) throw new ArgumentException("Below minimum.");
                if (max != null && i > int.Parse(max)) throw new ArgumentException("Above maximum.");
                break;
            case "boolean":
                _ = bool.Parse(value);  // throws FormatException if invalid
                break;
        }
    }
}
```

**Usage in FeeCalculationService (example — no hardcoded values):**
```csharp
public async Task<SessionFee> CalculateAsync(ParkingSession session, Zone zone)
{
    var incrementMin = await _settings.GetIntAsync("pricing.billing_increment_minutes");
    var minFee       = await _settings.GetDecimalAsync("pricing.minimum_fee");
    var discountPct  = await _settings.GetDecimalAsync("pricing.disability_discount_pct");
    var maxPenalty   = await _settings.GetDecimalAsync("overstay.max_penalty_cap");

    // Zone-level rate override takes priority over system default
    var baseRate = zone.HourlyRate  // set in zone_pricing_config
        ?? await _settings.GetDecimalAsync("pricing.base_hourly_rate_default");

    // ... rest of calculation using variables only
}
```

---

### 24.4 Settings API Endpoints

All under `/api/settings`. All require **SystemAdmin** role.

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/settings` | List all settings, grouped by category |
| `GET` | `/api/settings/{key}` | Get a single setting |
| `PUT` | `/api/settings/{key}` | Update a setting value (triggers cache invalidation) |
| `GET` | `/api/settings/zones/{zoneId}` | Get zone-specific pricing overrides |
| `PUT` | `/api/settings/zones/{zoneId}` | Update zone-specific pricing |
| `POST` | `/api/settings/cache/invalidate` | Manual cache flush (admin escape hatch) |

---

### 24.5 React — System Admin Settings Page

The settings page renders dynamically from the API response — no hardcoded fields in
the UI. Each setting's `data_type`, `min_value`, `max_value`, and `description`
drive which input component is rendered.

```tsx
// web/src/modules/user-access/SystemSettingsPage.tsx

const inputForType = (setting: SystemSetting, onChange: (v: string) => void) => {
  switch (setting.dataType) {
    case 'boolean':
      return <Toggle checked={setting.value === 'true'} onChange={v => onChange(String(v))} />;
    case 'integer':
      return <NumberInput value={setting.value} min={setting.minValue}
               max={setting.maxValue} step={1} onChange={onChange} />;
    case 'decimal':
      return <NumberInput value={setting.value} min={setting.minValue}
               max={setting.maxValue} step={0.01} onChange={onChange} />;
    default:
      return <TextInput value={setting.value} onChange={onChange} />;
  }
};
```

**Layout — grouped by category with live preview:**

```
┌─────────────────────────────────────────────────────────────────┐
│  System Settings                           ✓ Saved 2 min ago    │
├─────────────────┬───────────────────────────────────────────────┤
│ 💰 Pricing       │  Base Hourly Rate (Default)                   │
│ 📈 Surge         │  ┌──────────┐  LKR / hr                       │
│ ⚠️ Overstay      │  │  300.00  │  Applies when a zone has no     │
│ 📅 Booking       │  └──────────┘  custom rate configured.        │
│ 🔔 Notifications │                                               │
│ 🔐 Security      │  Billing Increment (minutes)                  │
│ 🖥️ System        │  ┌────┐  mins  ── min: 5 · max: 60            │
│                  │  │ 15 │                                       │
│                  │  └────┘                                       │
│                  │                                               │
│                  │  Disability Discount                          │
│                  │  ┌────┐  %     ── min: 0 · max: 50            │
│                  │  │ 15 │                                       │
│                  │  └────┘                                       │
│                  │                           [Save Pricing]      │
└─────────────────┴───────────────────────────────────────────────┘
```

Changes are saved per-category to avoid race conditions when multiple admins edit.

---

### 24.6 Seed Data (EF Core — required for demo)

All defaults must be seeded so the app works out of the box with `make setup`:

```csharp
// api/src/Infrastructure/Seeders/SystemSettingsSeeder.cs
public static class SystemSettingsSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var settings = new[]
        {
            new SystemSetting("pricing.base_hourly_rate_default", "300.00", "decimal",
                "pricing", "Base Hourly Rate (Default)", min: "0", max: "10000"),
            new SystemSetting("pricing.billing_increment_minutes", "15", "integer",
                "pricing", "Billing Increment (minutes)", min: "5", max: "60"),
            new SystemSetting("pricing.minimum_fee", "75.00", "decimal",
                "pricing", "Minimum Session Fee", min: "0", max: "1000"),
            new SystemSetting("pricing.disability_discount_pct", "15", "decimal",
                "pricing", "Disability Discount (%)", min: "0", max: "50"),
            new SystemSetting("pricing.currency_code", "LKR", "string",
                "pricing", "Currency Code (ISO 4217)"),
            new SystemSetting("surge.occupancy_trigger_pct", "80", "integer",
                "surge", "Surge Trigger Occupancy (%)", min: "50", max: "100"),
            new SystemSetting("surge.max_multiplier_cap", "3.00", "decimal",
                "surge", "Maximum Surge Multiplier", min: "1.0", max: "10.0"),
            new SystemSetting("surge.cooldown_after_rejection_minutes", "60", "integer",
                "surge", "Surge Re-proposal Cooldown (minutes)", min: "15", max: "1440"),
            new SystemSetting("overstay.grace_period_minutes", "15", "integer",
                "overstay", "Overstay Grace Period (minutes)", min: "0", max: "60"),
            new SystemSetting("overstay.penalty_fixed_amount", "500.00", "decimal",
                "overstay", "Base Overstay Fine (LKR)", min: "0", max: "10000"),
            new SystemSetting("overstay.max_penalty_cap", "2000.00", "decimal",
                "overstay", "Maximum Penalty Cap (LKR)", min: "0", max: "50000"),
            new SystemSetting("overstay.warning_lead_minutes", "10", "integer",
                "overstay", "Expiry Warning Lead Time (minutes)", min: "1", max: "60"),
            new SystemSetting("booking.max_advance_days", "30", "integer",
                "booking", "Max Advance Booking (days)", min: "1", max: "365"),
            new SystemSetting("booking.free_cancel_window_minutes", "60", "integer",
                "booking", "Free Cancellation Window (minutes)", min: "0", max: "1440"),
            new SystemSetting("booking.no_show_release_minutes", "20", "integer",
                "booking", "No-Show Auto-Release (minutes)", min: "5", max: "120"),
            new SystemSetting("booking.max_active_bookings_per_driver", "2", "integer",
                "booking", "Max Concurrent Bookings per Driver", min: "1", max: "10"),
            new SystemSetting("auth.jwt_expiry_hours", "24", "integer",
                "auth", "JWT Access Token Expiry (hours)", min: "1", max: "168"),
            new SystemSetting("auth.max_failed_login_attempts", "5", "integer",
                "auth", "Max Failed Login Attempts", min: "3", max: "20"),
            new SystemSetting("auth.lockout_duration_minutes", "15", "integer",
                "auth", "Account Lockout Duration (minutes)", min: "5", max: "1440"),
            new SystemSetting("notify.session_expiry_reminder_minutes", "30", "integer",
                "notify", "Session Expiry Reminder (minutes before end)", min: "5", max: "120"),
            new SystemSetting("notify.email_on_booking_confirm", "true", "boolean",
                "notify", "Email on Booking Confirmation"),
            new SystemSetting("notify.push_on_surge_activated", "true", "boolean",
                "notify", "Push Notification on Surge Activation"),
            new SystemSetting("system.maintenance_mode", "false", "boolean",
                "system", "Maintenance Mode"),
            new SystemSetting("system.allow_new_registrations", "true", "boolean",
                "system", "Allow New Registrations"),
            new SystemSetting("system.settings_cache_ttl_seconds", "30", "integer",
                "system", "Settings Cache TTL (seconds)", min: "5", max: "3600"),
            new SystemSetting("system.rate_limit_per_ip_per_minute", "100", "integer",
                "system", "API Rate Limit (requests/min per IP)", min: "10", max: "1000"),
        };

        foreach (var s in settings)
        {
            if (!await db.SystemSettings.AnyAsync(x => x.Key == s.Key))
                db.SystemSettings.Add(s);
        }
        await db.SaveChangesAsync();
    }
}
```

---

### 24.7 Validator Agent Integration — Reading Config

The Validator Agent in Python must also respect config caps (e.g., `overstay.max_penalty_cap`).
It fetches them via a dedicated internal endpoint, not from the database directly:

```python
# ai/tools/config_tools.py
async def get_config(key: str) -> str:
    """Validator Agent tool: fetch a system setting value."""
    async with httpx.AsyncClient() as client:
        r = await client.get(
            f"{API_BASE_URL}/api/settings/{key}",
            headers={"X-Internal-Token": INTERNAL_API_TOKEN}
        )
        r.raise_for_status()
        return r.json()["value"]

# Usage in validator agent node:
max_penalty = Decimal(await get_config("overstay.max_penalty_cap"))
if proposal.penalty_amount > max_penalty:
    return {"status": "REJECTED", "reason": f"Proposed penalty exceeds system cap of {max_penalty}"}
```

A dedicated `X-Internal-Token` header (separate from user JWT) authenticates the AI
service to the ASP.NET Core API. Add `INTERNAL_API_TOKEN` to GitHub Org secrets.

---

### 24.8 ADR Update

| # | Decision | Choice | Rationale |
|---|---|---|---|
| ADR-009 | Configuration storage | **`system_settings` DB table + IMemoryCache** | Zero hardcoded business values; admin-editable at runtime; cache prevents N+1 DB hits |
