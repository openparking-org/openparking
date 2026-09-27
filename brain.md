# OpenParking Project Context (Handoff)

## Current State & Completed Work
The project is on the `develop` branch.
The solution (`OpenParking.sln`) follows a clean architecture (`Core`, `Infrastructure`, `Api`, `Tests`) and **compiles cleanly with 0 errors**.
Phases 1-4 of the backend implementation are **complete**.

### Completed Implementations:
1. **Core Domain:** Entities are aligned. Key tracking fields (`CreatedAt`, `UpdatedAt`, `CreatedBy`) are standard across entities. `WorkflowStatus` and `PenaltyStatus` use enums.
2. **Service Layer (`OpenParking.Infrastructure`):**
    * **`UserService`:** Registration (BCrypt), Login (JWT), Profile management, Disability permit lifecycle.
    * **`ZoneService`:** Zone & Slot CRUD, FloorPlan upserts (with JSON waypoint validation), Real-time occupancy stats.
    * **`BookingService`:** Transactional bookings (overlap checks), QR session check-in/out, 15-minute billing logic, ANPR simulator hooks.
    * **`EnforcementService`:** Overstay detection loops, LangGraph AI workflow triggering, Penalty lifecycle, immutable Audit Logging.
    * **`EmailService`:** Resend REST API integration with retry and dead-letter queue logic.
3. **Real-time Notifications:** Implemented cleanly. `IRealtimeNotifier` (in Core) is implemented by `SignalRNotifier` (in Api) which wraps `SlotHub`. This breaks the circular dependency between Infrastructure and Api.
4. **Tests:** Stale field references in `OpenParking.Tests` (e.g., string statuses -> enum, `ResolvedAt` -> `ApprovedAt`) have been fixed.

## Next Steps (Phases 5+)
The next agent should pick up the work starting from Phase 5.

### Phase 5: Controller Refactoring
*   **Current State:** Controllers in `OpenParking.Api/Controllers` are either stubs or tightly coupled to `AppDbContext` (e.g., `WorkflowsController`).
*   **Action:** Refactor all controllers to inject and use the new domain services (`IUserService`, `IZoneService`, `IBookingService`, `IEnforcementService`).
*   **Standard:** Controllers must return `ApiResponse<T>` and rely on global middleware to catch `AppException` for business errors.

### Phase 6: Database Migrations & Seeding
*   **Action:** Generate the initial/updated migration: `cd api && dotnet ef migrations add Phase4Schema`.
*   **Action:** Update the local PostgreSQL database: `dotnet ef database update`.
*   **Action:** Implement seeding logic (users, realistic zones, slots) so the application is ready for testing.

### Phase 7: UI Integration (Flutter/Web)
*   Once the backend is fully refactored and seeded, begin connecting the frontend clients as outlined in `design.md`.

## Key Technical Decisions & Constraints
*   **Branching:** We are pushing to `develop`. We use specific branches and PRs to prove individual contributions (e.g., Karuna's branch).
*   **Error Handling:** Throw `AppException(errorCode, message)` for business logic failures.
*   **Audit Logging:** `AuditLog` is append-only. Use `WriteAuditLogAsync` (or equivalent) in services for mutations.
*   **Pagination:** Use `PaginatedQuery` (has `Skip`, `Take`, `SortDir` helpers) with the `ToPagedResultAsync` extension in `OpenParking.Infrastructure.Extensions`.
*   **AI Orchestration:** `EnforcementService` uses a configured `HttpClient` (30s timeout) to call the LangGraph service via `AI_SERVICE_URL`.
