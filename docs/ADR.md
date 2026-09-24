# Architecture Decision Records (ADRs)

## ADR-001: OpenMRS-Inspired Modular Service Architecture
- **Status:** Accepted
- **Context:** The team of 4 requires clear bounded contexts without merge friction across the vertical slices (User, Space, Booking, Enforcement).
- **Decision:** ASP.NET Core solution structured with modular slices communicating via standard interfaces (`IParkingModule`).
- **Consequences:** Clean separation of concerns, high testability, and isolated ownership per student.

## ADR-002: Cloudflare Workers as Edge API Gateway
- **Status:** Accepted
- **Context:** Protecting the Oracle Cloud VM backend from direct exposure, handling rate limiting, CORS, and early JWT validation without consuming compute resources.
- **Decision:** Cloudflare Worker deployed in front of the ASP.NET Core API backend.
- **Consequences:** Sub-millisecond global routing, zero compute cost on origin for blocked requests.

## ADR-003: LangGraph for Stateful Multi-Agent Orchestration
- **Status:** Accepted
- **Context:** Need transparent, auditable agent workflows (Dynamic Pricing, Overstay Enforcement, Disability Permit Validation) with Human-in-the-Loop approval checkpoints.
- **Decision:** Python service using LangGraph with state persistence in PostgreSQL.
- **Consequences:** Deterministic state machine graph, auditable transitions, pause-and-resume approval mechanics.

## ADR-004: State Management: Zustand (React) & Riverpod 2 (Flutter)
- **Status:** Accepted
- **Context:** Predictable state management with minimal boilerplate for fast viva comprehension.
- **Decision:** Zustand for React Admin Web Dashboard, Riverpod 2 for Flutter Mobile App.
- **Consequences:** Clean decoupled state with compile-time safety.
