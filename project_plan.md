# OpenParking Implementation Plan

This document outlines the 9-week agile implementation plan for the OpenParking system, broken down into 4 phases.

## Phase 1: Infrastructure & Scaffolding (Week 1-2)
**Goal:** Establish the foundation so all 4 members can start coding independently.
*   [ ] Initialize ASP.NET Core Web API project (with EF Core and PostgreSQL connection).
*   [ ] Initialize React Admin Dashboard (Vite + TS + Tailwind).
*   [ ] Initialize Flutter Mobile App.
*   [ ] Initialize Python AI Service (LangGraph + FastAPI).
*   [ ] Setup Docker Compose for local development.
*   [ ] Setup GitHub Actions CI pipelines for all 4 stacks.

## Phase 2: Core Domain & Database Models (Week 3-4)
**Goal:** Implement the database schema and basic CRUD APIs.
*   [ ] **Yowun (User Access):** `users`, `disability_permits`, `system_settings` tables and endpoints. JWT Auth integration.
*   [ ] **Supun (Space):** `zones`, `parking_slots`, `floor_plans` tables. React-Konva editor baseline.
*   [ ] **Dev (Booking):** `bookings`, `parking_sessions` tables. Fee calculation logic.
*   [ ] **Karuna (Enforcement):** `agent_workflow_runs`, `zone_pricing_rules` tables.

## Phase 3: Cross-Platform UI & Agentic AI (Week 5-7)
**Goal:** Connect the frontends to the APIs and implement the AI workflows.
*   [ ] **React:** Connect admin views for user management, lot management, and workflow approvals.
*   [ ] **Flutter:** Implement QR Scanner, GPS Maps, and Push Notifications.
*   [ ] **Python AI:** Build the Validator, Analyzer, Action, and Planner agents using LangGraph.
*   [ ] **Python AI:** Implement the deterministic A* Routing service.

## Phase 4: Integration, Polish & Deployment (Week 8-9)
**Goal:** End-to-end testing and production deployment.
*   [ ] Implement SignalR for real-time occupancy updates across React and Flutter.
*   [ ] Deploy PostgreSQL to Neon.
*   [ ] Deploy React to Cloudflare Pages.
*   [ ] Deploy ASP.NET API & Python AI to Oracle VM via Cloudflare Tunnels.
*   [ ] Final bug fixing and viva preparation.
