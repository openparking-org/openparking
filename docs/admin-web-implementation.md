# Admin web implementation

Implemented 4 October 2026. The web application is restricted to ParkingAdmin and SystemAdmin accounts. Driver accounts remain available to the existing backend/mobile application; administrators select a driver when creating a reservation.

| Page | Delivered behavior |
| --- | --- |
| Login | Admin role checks, authenticated session verification, clear authentication errors. |
| Dashboard | Backend occupancy, recorded charges, permit counts and operational links. |
| Zones | Search, pagination, create/edit/delete, mapping links; backend prevents deleting booking history. |
| Slot mapping | Floor image and metadata, editable bay positions and navigation graph, stable bay IDs, persisted layout, reviewed AI suggestions, unsaved-change protection. |
| Create reservation | Existing driver selection, zone/floor/bay selection, live bay updates, time and vehicle details, server-confirmed reservation. |
| Reservations | Search/filter, check-in, check-out, cancellation and server-calculated charges. Pay records a persistent Paid simulation for completed reservations. |
| Analytics | Backend charts and period selection, current occupancy, recorded charges. Charges do not represent provider-settled payments. |
| AI enforcement | Workflow history, proposal evidence, approval/rejection, overstay scan and retry of failed workflows. Python execution contract corrected; actual actions require admin approval. |
| Hardware | Persisted searchable device events and event recording. Physical devices must send their events to the API; no device provisioning or automatic heartbeat monitor was added. |
| Permits | Status filters, document links, AI validation through the authenticated backend, verified/rejected decisions with review notes. |
| Settings | SystemAdmin-only persisted policy edits; legacy grace-period and hourly-penalty aliases synchronized. |
| Users | SystemAdmin-only role management; prevents self-demotion and removal of the last active SystemAdmin. |

Shared changes include one authenticated API client, loading/error/empty states, pagination, immediate server-side account/role validation, protected registration roles and floor-layout safety checks. Older page exports delegate to the connected implementations.

## Verification

- Backend: 68 tests passed, including admin endpoint authorization, persisted simulated Pay, stable layout saves, booking ownership and the Python enforcement request/response contract.
- Frontend: 11 tests passed; TypeScript, ESLint and production build passed.
- Live browser: admin login, dashboard, analytics, zone creation, mapping save, reservation creation, check-in, check-out and Pay. Paid remained visible after reload. Permit history and settings pages rendered backend data.
- Live API: all admin list endpoints, settings save/readback with original value restored, hardware event save/readback, overstay scan, manager settings-write rejection and driver admin-endpoint rejection.

Local verification created the zone `QA-ADMIN-1004`, bay `F0-1`, a completed paid test reservation for vehicle `QA-1004`, and a hardware heartbeat test event. These records remain available for inspection. No external payment service or customer web flow was added.

AI proposal contract tests use a controlled service response. Physical sensors, external AI-provider quality, and a live approval of an actual overstay proposal were not verified end to end.
