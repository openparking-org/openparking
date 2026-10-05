# Web app functionality and backend integration plan

Reviewed: 4 October 2026. Scope: every route in `web/src/App.tsx`, the additional page components, shared authentication/API code, .NET controllers, SignalR hub, and relevant Python endpoints.

This is a source-code review and implementation plan. Pages and API behavior have not been verified in a running browser or against a live database. Existing uncommitted application changes are preserved. Endpoint availability means a controller exists, not that its complete behavior has passed integration testing.

## Accepted implementation scope

The subsequent user instruction supersedes the customer-facing and payment-provider proposals below: this web app is for ParkingAdmin and SystemAdmin users. Reservations are created by administrators for existing drivers. Pay marks a completed reservation as paid in a persistent simulation; it does not process money. See [implementation and verification](admin-web-implementation.md) for the delivered behavior. The remaining sections preserve the original pre-implementation review.

## 1. Current state and priorities

The web app has nine protected routes plus login. Some features call the backend, but others use mock data, incomplete contracts, or local state. Three additional page components are not routed: BookingManagementPage, SystemSettingsPage, and FloorPlanEditor.

Fix first:

1. Remove simulated booking confirmations and misleading slot-save success messages. Failed requests must remain failures.
2. Separate driver and admin routes. Currently `/book-parking` is inside the ParkingAdmin/SystemAdmin guard, so drivers cannot use it. Drivers redirected to `/` after login also encounter that guard.
3. Use one API configuration, authenticated client, and token source. The app mixes `VITE_API_BASE_URL`, `VITE_API_URL`, a hardcoded enforcement URL, and multiple token storage keys.
4. Persist settings through the API. Both the routed settings form and the unused SystemSettingsPage currently save locally or simulate a save. SettingsController has no controller/action authorization attributes; enforce SystemAdmin writes on the server and review read access.
5. Add the missing reservation/session views and backend list operations before claiming the whole parking journey works.

## 2. Existing pages: intended content and integration

### Login — `/login`

**Current:** Calls `POST /api/users/login` and stores the returned user/token. Redirect defaults to `/` regardless of role.

**Page content:** Email/password fields, validation, submitting state, clear invalid-credentials/network errors, and role-appropriate navigation after login.

**Work:** Restore sessions using `GET /api/users/me`; handle expired tokens, invalid persisted user data, and logout consistently. Return users to an authorized requested page; otherwise send admins to Dashboard and drivers to Book Parking. Prevent redirect loops and hide links users cannot access. Add a proper forbidden page and unknown-route page.

**Done when:** Each role can log in, reload, access its permitted pages, and sign out; expired credentials return to login without loops.

### Dashboard — `/`

**Current:** Renders AnalyticsDashboard, exactly like `/analytics`.

**Page content:** Operational overview: available/occupied/reserved bays, active sessions, overstays, pending permit reviews, pending AI decisions, today's recorded revenue, and links to act on each item. Show refresh time and connection state.

**Existing APIs:** `GET /api/analytics/summary`, `/api/analytics/occupancy`, `/api/analytics/workflows/summary`, `/api/agent/workflows/pending`, and `/api/users/permits/pending`.

**Work:** Create a distinct overview page. Reuse typed services rather than duplicate analytics charts. Define time boundaries for today's totals and label what revenue includes. Use existing API results initially; add an aggregate dashboard endpoint only if performance or missing metrics justify it. Link to the relevant filtered page rather than showing inactive cards.

**Done when:** Cards agree with source records and their links lead to usable workflows; a failed metric does not silently become zero.

### Parking Zones — `/zones`

**Current:** A table populated by App.tsx mock zones.

**Page content:** Searchable, paginated zone list; name/code/location; capacity and availability; rate/currency; zone detail with floor/slot overview; create/edit controls; safe removal controls; links to mapping and reservations.

**Existing APIs:** `GET/POST /api/zones`, `GET/PUT/DELETE /api/zones/{id}`, `GET /api/zones/{id}/occupancy`, and `GET /api/zones/{id}/slots/available`.

**Work:** Replace mock calls with authenticated services for writes. Match actual list pagination metadata rather than assuming every API uses the same paged envelope. Validate code, location, rate, and capacity. Define deletion rules for zones with bookings, sessions, or floor plans. Ensure displayed capacity matches the intended backend definition.

**Done when:** A created/edited zone survives reload, appears in booking and mapping, and has accurate counts. Invalid or unsafe deletions are rejected with useful explanations.

### AI Enforcement — `/ai-enforcement`

**Current:** Fetches pending workflows/details and submits approve/reject decisions. API base is hardcoded to localhost. Proposal rendering can substitute a fabricated fine when an amount is missing.

**Page content:** Pending decision queue; type, zone, vehicle/session context, creation time; structured proposed action and actual amount/currency; evidence, policy calculation, explanation; approve/reject with reason; resulting execution status.

**Existing APIs:** `GET /api/agent/workflows/pending`, `GET /api/agent/workflows/{id}`, and `POST /api/agent/workflows/{id}/approve` or `/reject`.

**Work:** Use the shared client; parse actual workflow result structures with explicit unavailable states. Remove fabricated fine defaults. Distinguish penalty and pricing proposals. Require a rejection reason, disable duplicate submissions, refresh after decisions, and handle another admin deciding first. Add filtered workflow history API/UI for completed, failed, and rejected runs; only pending retrieval exists today. Align analytics proposal timestamps with the controller's `createdAt` contract.

**Done when:** A real proposal can be inspected and decided once, its execution result is visible, and affected penalties/prices update. Missing evidence never appears as a real fine.

### Analytics — `/analytics`

**Current:** Charts and summary cards already call backend services; authentication also supports legacy/manual token storage.

**Page content:** Date-range selection; recorded revenue trend; occupancy by zone; session duration and overstay metrics; weekly violations; workflow outcomes; definitions and last refresh time. Export can follow after core reporting works.

**Existing APIs:** `GET /api/analytics/summary`, `/revenue/daily`, `/occupancy`, `/violations/weekly`, and `/workflows/summary`.

**Work:** Keep one auth source and remove manual-token entry from the normal product flow. Align DTOs, date ranges, currency, and workflow timestamps. Label occupancy as current unless historical data is implemented. Handle partial request failures without presenting stale figures as current. Verify whether revenue represents calculated charges or settled payments and label accordingly.

**Done when:** Selected periods affect all applicable charts; totals reconcile with underlying records; empty periods, partial failures, and mixed currencies are handled explicitly.

### Hardware Logs — `/hardware`

**Current:** App.tsx displays three mock messages. No hardware-log controller was found among the reviewed .NET controllers.

**Page content:** Devices by zone; sensor/camera identifiers; online/offline state and last seen; timestamped events with severity/type; zone/device/date filters; event details; pagination. Display battery readings only if collected.

**Backend additions:** Device registration/status persistence, event ingestion, heartbeat processing, and authorized paginated device/event read APIs. Endpoint names should be finalized with the backend contract. Existing `/api/simulate/entry` and `/exit` provide development event simulation, not a hardware monitoring API.

**Work:** Define event types and status rules, attach server timestamps, connect the UI, and distinguish disconnected devices from occupied bays. Use polling initially if no event stream exists.

**Done when:** An ingested event is visible and filterable, a device's last-seen status changes correctly, and empty/error states replace sample messages.

### Permits Review — `/permits`

**Current:** Loads pending permits and sends review decisions. The All filter only filters the pending response. AI validation is sent through the .NET-base client even though the endpoint is implemented in Python. The UI uses Approved where the domain uses Verified.

**Page content:** Pending and historical permit lists; holder identity; permit number, jurisdiction, expiry, and secure document preview; AI recommendation/confidence/reasons; manual verification/rejection notes; reviewer and decision timestamps.

**Existing APIs:** `GET /api/users/permits/pending`, `PATCH /api/users/permits/{permitId}/review`. Python exposes `POST /ai/permits/validate`.

**Work:** Map UI labels to actual PermitStatus values and confirm request/response types. Add an all-status paginated review endpoint and a DTO with holder information rather than assuming a nested user is returned. Prefer an authenticated .NET proxy to Python so deployed browser requests use one gateway and decisions are auditable; explicitly configure routing if retaining direct AI calls. Preview protected documents, show fetch failures, prevent double review, and treat AI output as a recommendation unless a persisted automatic decision exists.

**Done when:** A submitted permit appears, its document can be reviewed, the decision persists, approved access rules reflect it, and historical filters include actual reviewed records.

### Settings — `/settings`

**Current:** Routed form changes SettingsContext only. The unused SystemSettingsPage contains hardcoded policy rows and a fake database-save timeout.

**Page content:** Grouped pricing, currency, overstay, permit, and AI policy fields; current values; descriptions/units; numeric limits; unsaved-change state; save result. Display actual server-supported keys.

**Existing APIs:** `GET /api/settings`, `GET /api/settings/{key}`, `PUT /api/settings/{key}` with `{ value }`.

**Work:** Consolidate the two forms into one persisted page. Load grouped settings and use seeded canonical keys, including `pricing.default_currency`. Resolve overlapping aliases/caps before exposing conflicting policy controls. Add server-side validation and SystemAdmin write authorization; derive audit identity from claims rather than trusting client `updatedBy`. Refresh shared currency/policy state after success. Either save each key explicitly or add a transactional bulk-save endpoint for interdependent settings.

**Done when:** Changes survive reload and affect the intended calculations; invalid values are refused; ParkingAdmin and Driver cannot update policy through direct API requests.

### Slot Mapping — `/slot-mapping`

**Current:** Loads zones/slots, supports manual/AI/indoor modes, and posts slot batches. Batch save omits the bearer token and reports success even on rejected requests. Floor-plan imagery/anchors include demo defaults.

**Page content:** Zone/floor selectors; outdoor map or indoor blueprint; slot geometry, number/type, and device assignments; add/edit/remove tools; AI detection preview and review; save status, unsaved-change warning, and reload.

**Existing APIs:** Zone detail; `POST /api/zones/{id}/slots/batch`; individual slot creation/status APIs; `GET /api/zones/{id}/floor-plans`; `GET /api/zones/floor-plans/{floorPlanId}`; `POST /api/zones/{id}/floor-plans`. Python: `POST /ai/cartographer/detect`.

**Work:** Authenticate writes and expose actual save errors. Persist floor-plan images/anchors and use server IDs returned from saves. Define explicit create/update/delete semantics: repeatedly posting all slots to a create endpoint must not create duplicates. Add geometry edit/delete endpoints if the current API cannot persist those actions. Validate duplicate numbers, geometry, floor associations, types, and device assignments. Do not store occupancy as map-design state. Show genuine AI confidence/results and require review before saving detections. Configure map credentials and provide a useful missing-key error.

**Done when:** Map changes and floor plans survive reload; repeated saves are safe; booking sees the same persisted slots; failed requests never display Saved.

### Book Parking — `/book-parking`

**Current:** Reads zones/slots, subscribes to live updates, and creates bookings, but replaces API failures with simulated successful bookings. Accessible only to admins under current routing.

**Page content:** Zone and floor selection; live slot availability/type; start/end time; plate; price estimate with currency and fee breakdown; accessible-bay eligibility; confirmation with persisted reference; links to booking detail and My Bookings.

**Existing APIs:** Zones/detail/available slots; `POST /api/bookings`; `GET /api/bookings/{id}`; `/hubs/slots` events using `JoinZoneGroup` and `SlotUpdated`.

**Work:** Make this a driver route. Admin-created bookings need an explicit on-behalf-of contract because the current controller uses the signed-in user's identity. Remove demo fallbacks and fabricated confirmations. Validate times/plate on both sides, use server pricing as authoritative, and add a quote API if required for an accurate preview. Define availability for the selected time interval rather than relying solely on current occupancy. Enforce atomic booking conflict prevention and permit eligibility on the server. Resynchronize after SignalR reconnect and handle reservation conflicts clearly.

**Done when:** A real driver reserves an eligible slot, sees a persistent booking after reload, and a competing request receives a conflict rather than another confirmation.

## 3. Additional components and missing journey pages

### Reservations and Sessions — proposed `/bookings` for admins

BookingManagementPage exists but is unmounted and displays mock records. Replace it with a real paginated table: booking reference, driver/plate, zone/slot, time window, booking/session status, charge, and detail actions. Add filters for zone, status, date, and plate.

Existing `/api/bookings/user` returns only the current user's bookings. Add authorized admin booking/session list and detail DTOs; define cancellation rights for admins. Connect `POST /api/sessions/check-in`, `/check-out`, `GET /api/sessions/active`, and `/api/sessions/{id}` to permitted operations. Review session ownership and role checks before exposing actions.

Acceptance: Admins can locate a reservation, check in/out through the defined process, and see timing, slot state, and final charge update consistently.

### My Bookings and booking detail — proposed `/my-bookings`, `/my-bookings/:id`

Use `GET /api/bookings/user`, `GET /api/bookings/{id}`, and `POST /api/bookings/{id}/cancel`. Show upcoming/active/completed/cancelled records, persistent reference, reservation times, slot, charge, and valid cancellation actions. Add a QR pass only after defining a server-verifiable pass/token contract.

The existing booking and zone route endpoints return dummy waypoints. Replace them with real floor-plan/pathfinding integration before exposing indoor navigation. Validate ownership and zone/slot relationships.

Acceptance: Refresh preserves the booking, cancellation releases availability according to policy, and only the owner or authorized admin can read details.

### Driver profile and permit submission — proposed `/profile`, `/my-permit`

Use `GET /api/users/me`, `POST /api/users/{id}/permits`, and `GET /api/users/me/permit`. Show user identity and current permit status; submission needs permit details, expiry, jurisdiction, and document upload. The existing submit contract takes document metadata/URL, so define storage/upload and protected retrieval rather than assuming file upload exists. Add registration at `/register` using the existing registration endpoint if driver self-service is in scope.

Acceptance: A driver submits a document, sees Pending, and later sees the actual review outcome; another user's identity cannot be used to submit/read a permit.

### Payments and receipts — proposed booking-linked detail flow

No payment/receipt controller was found in the reviewed API controller set. Define whether the first release records charges only or processes actual payments. For real payments, add payment initiation/status, provider callback verification, idempotency, reconciliation, and receipt retrieval; choose the provider before implementation. Display calculated, pending, paid, failed, and refunded states distinctly.

Acceptance: Payment confirmation comes from verified server/provider state; retries do not double-charge; receipt totals match the session's server-calculated charge. If deferred, label charges accurately and never imply money was collected.

### Driver penalties — proposed `/my-penalties`

Use `GET /api/penalties/my` and `POST /api/penalties/{id}/dispute`. Show amount/currency, session, reason, status, and dispute text/status. Add an admin dispute-review API/page if dispute resolution is part of the release; the driver dispute endpoint alone does not provide that workflow.

### User administration — proposed `/users`, SystemAdmin only

Existing API supports `GET /api/users`, `GET /api/users/{id}`, and `PATCH /api/users/{id}/role`. Add search/pagination, profile details, and controlled role changes. Define protections against removing the final SystemAdmin and how existing sessions react to a role change.

### FloorPlanEditor — consolidate into Slot Mapping

This unmounted component has local sample slots/waypoints; waypoint placement and Save Floor Plan are incomplete. Integrate its intended tools into the mapping page rather than adding a competing editor. Persist navigation geometry/edges and ensure floor-plan read APIs return the data needed to reopen the editor; current summary responses expose image/anchor metadata rather than a complete navigation graph.

## 4. Shared implementation requirements

- One typed API layer: central base URL, token, response envelope handling, pagination adapters, error handling, abortable requests, and domain DTOs matching backend enums/fields.
- One auth store: validate persisted sessions; clear all relevant state on logout; enforce permissions server-side and show only authorized navigation.
- Explicit states on every page: loading, empty, error/retry, submitting, success, and stale/disconnected data. Never substitute demo records for production request failures.
- UTC request/storage timestamps with local display; define reporting timezone and validate start/end ordering. Currency comes from server contracts; financial amounts use consistent server rounding.
- SignalR uses central configuration; rejoin groups and refetch snapshots after reconnect; dispose old subscriptions when changing zone/page. Initial connection failure must be visible with retry or polling behavior.
- Route Python capabilities through a documented gateway/proxy, with appropriate authorization, timeouts, and useful unavailable errors. Inspect worker routing/CORS when finalizing deployment configuration.
- Validate DB migrations, development seed accounts/roles, CORS, API/AI URLs, map key configuration, and gateway paths in the intended environment. Keep secrets on the server.
- Accessible inputs/actions and usable desktop/mobile layouts; replace blocking alerts with inline messages where practical.

## 5. Implementation order and release gates

| Phase | Work | Exit condition |
|---|---|---|
| 1: Contracts and foundations | API/DTO inventory, auth/client/config consolidation, role routing, honest error handling, server policy authorization | All three roles navigate correctly; failed writes cannot claim success |
| 2: Parking inventory | Real zones CRUD, floor plans, persisted slot mapping, edit/delete semantics, realtime resync | Create zone -> save slots -> reload -> see same inventory |
| 3: Booking lifecycle | Driver booking, time-window availability, server quote, conflict prevention, My Bookings, admin booking/session views, cancellation/check-in/out | Two clients complete booking through checkout with consistent slot/session state |
| 4: Policy and review | Persisted settings, permit submission/review, AI proxy, enforcement decisions/history | Policy changes and admin decisions persist and affect actual records |
| 5: Operational reporting | Separate Dashboard/Analytics, reconcile metrics, hardware ingestion/logs, role administration | Reports match seeded transactions; real device events are visible |
| 6: Financial completion and release | Payment/receipt work if included, real navigation if included, full-flow verification | Agreed release flows pass against the actual backend without mock fallbacks |

Hardware and payment work can be separate workstreams after contracts are agreed; they should not block the initial inventory/booking integration. Explicitly record any deferred feature in the release scope.

## 6. Verification plan

Run the web type check, lint, tests, and production build plus relevant .NET/Python tests. Add integration coverage where it proves a business boundary: role/ownership enforcement, concurrent reservations, lifecycle transitions, settings persistence, and decisions applied once.

Verify in a browser against a real development database:

1. SystemAdmin creates a zone and floor plan, saves slots, reloads, and confirms persistence.
2. Driver logs in, selects a time window, reserves an eligible bay, reloads the confirmation, and finds it in My Bookings.
3. Another driver attempts a conflicting reservation; exactly one succeeds.
4. Check-in/checkout update the session, fee, and slot; a second browser observes the live update.
5. Cancellation follows policy and updates availability.
6. A driver submits a permit; an admin reviews it; the driver sees the persisted result and eligibility changes.
7. An overstay proposal is reviewed; approval/rejection persists once and the driver/reporting views reflect the outcome.
8. Settings survive reload; API-level unauthorized writes fail; reports reconcile with the test records.
9. API outage, AI outage, expired auth, map-key absence, and SignalR reconnect all show usable recovery paths. No scenario creates a fabricated successful booking or save.
10. If included, a verified payment produces one charge and a matching receipt; navigation uses real saved floor geometry.

The app is functional when its agreed end-to-end flows persist in the backend, remain consistent across reloads and users, and provide honest failure/recovery states.
