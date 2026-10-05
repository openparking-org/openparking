# OpenParking — Google Stitch web UI design brief

Paste this document into Google Stitch. For smaller generations, use the shared prompt and design system with one screen batch at a time.

## Shared prompt

Design a complete, high-fidelity, responsive web application called **OpenParking**, a parking management platform. Create connected application screens with realistic data, reusable components, detail drawers, forms, and decision modals. Prioritize efficient daily operations, readable maps, and clear decisions.

The main web experience serves Parking Admins and System Admins. Include a secondary browser parking reservation flow with a simpler driver shell. The separate Flutter driver mobile app is outside this request.

Parking Admins manage zones, availability, slots, floor plans, bookings, sessions, accessibility permits, AI recommendations, analytics, and hardware events. System Admins additionally manage users, roles, global policies, integrations, and audit logs. Hide system-only navigation from Parking Admins and show the current role in the profile menu.

Design the intended complete product: dedicated user management, audit logs, and pricing approval screens are planned capabilities. Existing browser booking is currently inside the admin-protected route tree; the separate driver shell requested here is an intended improvement requiring routing and permissions work.

## Visual design system

Use a calm, precise dark dashboard with teal accents and parking maps as the signature visual. Use solid, readable surfaces, restrained shadows, and consistent outline icons.

| Token | Value |
| --- | --- |
| Background | #0B1220 |
| Sidebar | #0F172A |
| Cards | #162033 |
| Raised panels | #1E293B |
| Borders | #334155 |
| Primary text | #F8FAFC |
| Secondary text | #CBD5E1 |
| Muted text | #94A3B8 |
| Primary accent | #2DD4BF; dark #042F2E text on filled buttons |
| Available | #34D399 |
| Occupied | #FB7185 |
| Reserved | #FBBF24 |
| Offline | #94A3B8 |
| AI suggestions | #A78BFA |

Use Inter or a similar sans-serif, 28–32 px page titles, 18–20 px section titles, and 14–16 px body text. Use tabular numbers and monospace for plates and IDs. Use an 8 px spacing system, 12 px card corners, and 8 px input corners. Pair status colors with labels or icons. Accessible and EV are slot types, separate from availability; use wheelchair and charging icons without replacing availability information.

Logo: a simple P inside a rounded square beside OpenParking. Keep product copy practical and concise. Avoid student ownership labels, backend implementation details, and decorative elements that compete with operational information.

## Shared admin shell

Desktop reference: 1440 × 1000. Tablet: 1024 × 768. Mobile: 390 × 844.

Use a 248 px sidebar, 72 px top bar, and 24–32 px content padding. Sidebar groups:

- Operations: Overview, Parking Zones, Live Availability, Slot Mapping, Bookings & Sessions.
- Review: AI Approvals, Permit Reviews, with pending-count badges.
- Insights: Analytics, Hardware & Events.
- Administration, System Admin only: Users & Roles, System Settings, Audit Logs.

Top bar: location selector, search for plates/bookings/slots, live connection status, notifications, and profile menu. Search results link to records; notifications link to the relevant review or event. Sidebar footer shows user, role, and sign out.

Pages have a title, one-line description, filters, and contextual actions. Use consistent tables with search, sortable columns, pagination, visible row actions, and selected rows. Use drawers for inspection and modals for confirmations. Detail views and editors have breadcrumbs.

## Example data

Use Sri Lankan locations, LKR currency, and Asia/Colombo timezone. Format dates as 04 Oct 2026 and times as 14:30. Currency is configurable; LKR is the mockup example.

- Locations: Colombo City Centre, Liberty Plaza, Harbor Parking.
- Default selection: Colombo City Centre / Zone A / Ground Floor.
- Slot inventory: 120 total = 78 occupied + 14 reserved + 24 available + 4 offline. Occupancy: 65%; distinguish reserved from occupied.
- Types: Standard, Compact, EV, Accessible. Slot IDs: A-001, A-012, A-018, A-024.
- Driver: Nimal Perera; Toyota Aqua; plate WP CBA-1234.
- Booking BK-2048: A-012, 04 Oct 2026, 13:00–15:00, LKR 200/hour, estimated LKR 400.
- Enforcement snapshot at 15:35: this session is 35 minutes beyond its booked end and 20 minutes beyond a 15-minute grace period. Proposed penalty LKR 500, awaiting review.
- Additional drivers: Kavindi Silva / WP CAB-5678; Dilan Fernando / WP CBB-9012.
- Pending queue: 8 penalties, 2 pricing proposals, 3 permits.

Label timestamps so earlier reservation views and later enforcement views remain coherent. All content and policy amounts above are illustrative.

## 01. Administrator sign in

Split desktop layout: subtle parking blueprint illustration and product statement on the left; focused login card on the right. Mobile uses a centered card. Include email, password, show/hide password, Sign in, submitting state, invalid credentials, and expired-session message. Add a separate Book parking link. Do not invent social login providers.

## 02. Overview

Title Parking overview. Filters for location and date; View live map action. Four KPI cards: Available spaces 24, Occupancy 65%, Revenue today LKR 148,920, Awaiting review 13.

Main content: large live parking map and zone summary with all four availability counts. Below: occupancy by hour, daily revenue trend, pending-review rows with Review actions, and a recent activity feed. Show entrances, exits, bay IDs, legend, live indicator, and Updated 10 seconds ago. Queue actions open the relevant penalty, pricing, or permit review.

## 03. Parking zones

Searchable table or cards with zone name, location, floor count, total/available slots, occupancy bar, hourly rate, and operational state. Filters: location and state. Actions: Add zone, Edit, View availability, Map slots.

Zone details include overview, floors, slot inventory, and pricing. Add/Edit Zone modal: name, location, address, map position, opening hours, hourly rate, description. Show required-field validation. Deactivation confirmation explains affected reservations and prevents unsafe changes while sessions are active.

## 04. Live availability

Large interactive map, collapsible slot list, and selected-slot drawer. Controls: location, zone, floor, type, status, slot/plate search, zoom, reset view. Show drive lanes, entrances, exits, directions, bay IDs, type icons, and status legend.

Slot drawer: ID, type, supported vehicle size/dimensions, availability, assigned camera/sensor, last update, linked booking/session. Actions: View session and Edit slot where allowed. Provide a readable list alternative. Include live, reconnecting, and offline states; keep last-known data visible but explicitly stale during connection loss.

## 05. Slot mapping workspace

Focused editor with compact navigation. Top controls: location, zone, floor, Manual / AI-assisted / Indoor mode switcher, draft status, Save mapping. Left tool rail, large central canvas, right properties inspector.

Manual tools: select, draw bay polygon, move/resize, duplicate, remove, undo/redo, zoom, fit to view. Properties: slot number, type, supported vehicle size, floor, camera, sensor, coordinates. Validate duplicate IDs, overlapping bays, and missing fields.

AI-assisted: upload image or floor plan, Detect spaces, analysis progress, failure/retry, dashed suggestions with confidence labels. Allow adjustment, acceptance, and discard. Keep unconfirmed suggestions distinct from saved bays and require review before saving them.

Indoor: blueprint upload/replacement, floor tabs, bays, entry/exit nodes, waypoints, connected paths, and Preview route from entrance to A-012. Include unsaved-changes warning, saving, and success state.

## 06. Bookings & sessions

Tabs: All bookings, Upcoming, Active sessions, Completed, Cancelled. Filters: date, location, zone, status, driver/plate.

Table: booking ID, driver/plate, slot, booked window, check-in, elapsed time, estimated/final fee, booking state, payment state. Keep payment separate from booking state.

Detail drawer: driver, vehicle, slot, booking timeline, QR pass preview, entry/exit history, fee breakdown, receipt when available. Contextual actions: View slot, Cancel reservation, View incident. Overstay banner explains grace period. Completed sessions show final duration and charges. Failed cancellation retains the record and displays the error.

## 07. AI approvals — penalties

Tabs: Penalties, Pricing proposals, Workflow history. Pending counts and filters for status, location, severity, time. Table: incident ID, plate, zone/slot, overstay, proposed amount, confidence, status, Review.

Review layout: left column contains session facts, booked window, grace period, overstay, evidence with timestamp, violation history, and charge calculation. Show a labeled unavailable-evidence placeholder when needed. Right column contains recommendation, policy checks, penalty cap, and workflow timeline: Plan → Analyze → Propose → Validate → Awaiting review.

Sticky actions: Reject proposal and Approve penalty. Approval confirmation states amount and notification outcome. Rejection requires a reason. Show reviewer/time after success and notification delivery separately. High confidence does not imply automatic approval. Failed validation disables approval with a reason. Submission failure retains the review and entered notes.

## 08. AI approvals — pricing

List zone, occupancy, current price, proposed multiplier/rate, effective window, review state. Example: LKR 200/hour → LKR 250/hour at 1.25×.

Drawer: demand trend, rationale, configured maximum multiplier, affected zone, duration, and effect on reservations. Design assumption: confirmed reservations retain their booked rate, subject to actual backend policy confirmation. Include approval confirmation, rejection reason, and timestamped outcome. Pending prices must not appear active.

## 09. Accessibility permit reviews

Inbox with status tabs and applicant search. Rows: applicant, permit number, jurisdiction, expiry, submitted date, status.

Review: zoomable document preview on left; applicant/permit fields, expiry warning, AI validation result, reviewer notes on right. Actions: Run AI check, Approve permit, Reject permit. Rejection requires a reason. Include expired/unreadable document, checking, and AI unavailable states. Manual review remains available if AI checking fails. Display only details needed for the decision.

## 10. Analytics & reporting

Date/location/zone filters and Export report. KPI cards: revenue, average occupancy, completed sessions, compliance rate. Charts: daily revenue line, hourly occupancy heatmap, bookings by zone bars, violations by type bars. Include zone comparison table and clearly labeled revenue forecast with time range and uncertainty. Distinguish predictions from observed data. Include units, tooltips, text summaries, and no-data state.

## 11. Hardware & events

Cards: online cameras, online sensors, offline devices, recent entry/exit events. Device table: ID, type, location, connection state, last seen, battery where applicable. Device-detail drawer and searchable event feed with timestamp, severity, source, plate if relevant, message. Filters: device type, severity, location, time. Pause/resume feed. Label simulated ANPR events Simulator.

## 12. Users & roles

System Admin only. Search and filters for role/account state. Table: name, email, role, state, created date. User drawer and role editor with Driver, Parking Admin, System Admin.

Role changes and deactivation require confirmation identifying the user and access impact. Prevent removal of the last active System Admin. Include save failures and permission errors. Add-user form is a planned capability: full name, email, role, account state; do not invent an invitation-email service.

## 13. System settings

System Admin only. Section navigation: General, Pricing, Overstay policy, Permit validation, Integrations.

- General: currency, timezone, default location.
- Pricing: base hourly rate, maximum surge multiplier.
- Overstay: grace period, penalty rate, maximum cap.
- Permits: confidence threshold and review policy.
- Integrations: map/email/AI connection states, masked credentials, explicit reveal control, test-connection result.

Friendly labels, units, help text, validation, changed-field indicators, discard/reset, sticky Save changes bar, saved timestamp, and server-error state. Policy edits preview a sample charge. Penalties and pricing retain human approval even at high confidence.

## 14. Audit logs

System Admin only. Read-only table: timestamp, actor, role, action, entity/reference, outcome. Filters: date, actor, action, outcome, entity. Detail drawer: before/after comparison, linked record, decision reason. No edit/delete actions.

## 15. Secondary browser booking journey

Simpler shell: OpenParking, Find parking, My bookings, profile. Reuse visual tokens with less density. Create five connected screens:

1. Find parking: location/date/time, vehicle size, map and lot cards with address, availability, opening hours, price, accessible/EV features.
2. Choose a space: zone/floor selectors, large plan, status legend, booking summary with slot/type/time/duration/price. Block occupied, reserved, offline, and incompatible spaces. Accessible bays require an approved valid permit.
3. Review reservation: vehicle/plate, slot, time window, charges, cancellation terms, Confirm reservation. Payment presentation follows supported capabilities; do not invent card collection or a provider.
4. Confirmation: booking ID, QR pass, entrance instructions, window, total, View my booking. Mockup QR codes are illustrative placeholders.
5. My bookings and detail: upcoming/active/past records, pass, session timeline, time remaining, overstay warning, eligible cancellation, final receipt.

Handle a slot becoming unavailable before confirmation: This space was just reserved. Choose another space. Preserve search and vehicle details. Include no matching spaces, expired permit, incompatible size, submitting, and reservation failure.

## Responsive layouts and shared states

Desktop: full sidebar, rich tables, split reviews, wide canvases. Tablet: collapsed navigation, fewer table columns, inspector drawers. Mobile: navigation drawer, stacked cards, record cards instead of dense tables, full-screen detail views. Keep map controls reachable. Advanced mapping can offer a simplified view with Open on desktop to edit.

Use at least 44 px touch targets, readable contrast, labeled inputs, keyboard navigation, visible focus, reduced-motion support, and text/icon alternatives to color. Charts need text summaries and maps need a list alternative. Sticky actions must not cover content.

Design hover, focus, selected, disabled, skeleton loading, empty first-use, no results/Clear filters, recoverable error/Retry, live disconnect, expired session, access denied, and page not found. Distinguish empty data from failed requests. Use toasts for small successes, inline form errors, connection banners, and disabled submission buttons while saving. Retain input after failed saves. Never label stale data live.

## Stitch output and suggested batches

Generate all 15 screen groups, including related drawers, modals, forms, and booking sub-screens. Share buttons, inputs, badges, navigation, tables, charts, and overlay styles. Keep sample data and labels consistent.

Priority hero screens: Overview, Live Availability, Slot Mapping, Bookings & Sessions, Penalty Review, Permit Review, Choose a Space.

Connected journeys: dashboard → map → slot → session; queue → evidence → decision; zone → mapping → save; search → slot → review → confirmation.

For each batch, paste the Shared prompt, Visual design system, Shared admin shell, Example data, Responsive layouts and shared states, and relevant screen sections:

1. Foundation and operations: 01–04.
2. Mapping and bookings: 05–06.
3. Review workflows: 07–09.
4. Insights and administration: 10–14.
5. Browser reservation: 15 and its five screens.

Refinement prompt: Keep the established OpenParking design system and data. Refine [screen name] with its specified drawers, responsive layouts, loading/error states, and connected actions. Preserve navigation and component styles.

## Implementation handoff

The previous architecture document is preserved at `docs/architecture-design.md`.

Existing route references: `/login`, `/`, `/zones`, `/slot-mapping`, `/ai-enforcement`, `/permits`, `/analytics`, `/hardware`, `/settings`, `/book-parking`. Additional destinations require implementation. Existing components also cover floor-plan editing, booking management, and system settings; not all are wired into the current route tree.

Generated interfaces must be checked against actual API permissions, pricing policies, and payment capabilities before implementation.
