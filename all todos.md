# OpenParking — Remaining Core Modules TODOs

## 1. User & Access (Identity & Permits)
- [x] **Role Management & JWT Auth:** Securing endpoints for Drivers vs. Admins.
- [x] **Disabled Parking Verification:** The Validator Agent workflow to check permit data against regulatory schemas to automatically approve/reject disabled bay requests.

## 2. Booking & Payment (Driver Journey)
- [x] **Reservation Logic & Session Lifecycle:** Real-time handling of when a driver books a slot, checks in (scans QR), and checks out.
- [x] **Dynamic Pricing & Payments:** The Action Agent which calculates surge-price multipliers based on current lot capacity and triggers payment transactions.
- [x] **Resend Email Integration:** Sending booking confirmations and receipts natively from the .NET Core API.

## 3. Enforcement & AI Orchestration (Audit & Automation)
- [ ] **Overstay Detection Workflow:** A `.NET` BackgroundService that sweeps the database for expired sessions and triggers the LangGraph AI to propose a penalty.
- [ ] **Penalty Approvals:** The React UI where Parking Admins can review the AI Planner's proposed penalties and click "Approve" or "Reject".
- [ ] **Hardware Simulation (ESP8266/OpenCV):** Setting up the Python mock endpoints (`/simulate/entry` and `/simulate/exit`) that pretend to be the physical gate ANPR cameras opening and closing booking sessions.

## 4. The Flutter Mobile App (Driver UI)
- [ ] **Mobile Interface:** The entire mobile interface using Dart and Riverpod 2.
- [ ] **GPS Navigation:** Mapbox SDK integration for GPS navigation.
- [ ] **QR Code Scanner:** `mobile_scanner` integration for entry/exit.
- [ ] **Booking Tracking UI:** The active booking session tracking UI.
