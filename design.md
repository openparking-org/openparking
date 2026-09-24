# SmartPark: Integrated Parking Management System
## Architecture and Design Document
**Course:** SE3090 - Software Engineering Frameworks (Assignment 1)
**Group Size:** 4 Members
**Target:** BSc (Hons) in Information Technology, specializing in SE/AI

---

## 1. System Overview
SmartPark is a full-stack, AI-driven parking management system designed to handle real-time space allocation, vehicle size matching, dynamic pricing, and disabled parking verification. The system integrates edge hardware (ANPR via computer vision and physical gate actuation) with a secure, cross-platform software architecture. 

## 2. Technology Stack & Core Infrastructure
*   **Backend API:** ASP.NET Core Web API (Mandatory public backend, handles all business rules and validation).
*   **Database:** PostgreSQL with Entity Framework Core (normalized relational schema, transaction management).
*   **Web Dashboard:** React (Functional components, hooks, administrative interface for staff and AI workflow approvals).
*   **Mobile App:** Flutter (Driver-facing app for bookings, navigation, and live status).
*   **Agentic AI Engine:** LangGraph orchestrated locally via Ollama utilizing Qwen2.5-Coder models for complex reasoning.
*   **Hardware/Edge Layer:** Python microservice (OpenCV/EasyOCR) for ANPR detection, communicating with ESP8266 microcontrollers to actuate gate relays.
*   **Version Control & CI/CD:** GitHub Actions for automated testing and builds.

## 3. Component Ownership (4-Member Structure)
To satisfy the strict individual contribution requirement, the system is divided into four primary components:
1.  **User & Access Component:** Identity management, JWT authorization, profile handling, and disabled permit verification.
2.  **Space & Hardware Component:** Parking zone tracking, vehicle-to-slot size allocation, and integration with the ESP8266/Python ANPR gate endpoints.
3.  **Booking & Payment Component:** Reservation logic, external mapping/payment API integration, and session management.
4.  **Enforcement & Agentic AI Component:** Overstay tracking, penalty generation, and full orchestration of the Multi-Agent system.

## 4. High-Level Architecture (Modular / OpenMRS-Inspired)
The architecture follows a modular service-layer pattern.
*   **Entry Point:** The Flutter app submits a booking or an ESP8266 camera triggers a gate arrival event.
*   **Gatekeeper:** The ASP.NET Core API receives all requests, validates JWT tokens, and checks business rules against PostgreSQL.
*   **Service Layer:** Pluggable services handle specific logic (e.g., `AllocationService` filtering slots by vehicle size).
*   **Shared Status:** Once a booking or AI penalty is finalized, PostgreSQL updates the audit fields, reflecting the new status on both the React admin dashboard and the Flutter driver app.

## 5. Agentic AI Subsystem: Dynamic Pricing & Overstay Workflow
The AI subsystem executes a stateful, multi-step workflow. It is not a chatbot; it uses defined tools, validates inputs, and requires human authorization for high-impact actions.

### 5.1 The Four Distinct Agents
1.  **Planner Agent (Coordinator):** 
    *   **Responsibility:** Receives the domain objective (e.g., "Handle overstay for vehicle WP CBA-1234" or "Calculate dynamic surge pricing for Zone B"). Creates a structured execution plan and delegates tasks.
2.  **Domain Analysis Agent (Analyzer):**
    *   **Responsibility:** Queries the database (via allow-listed tools) to fetch contextual data. For overstays, it retrieves the user's past violation history. For pricing, it analyzes current lot traffic and booking velocity.
3.  **Action Agent (Tool Executer):**
    *   **Responsibility:** Executes deterministic functions. Generates a structured JSON proposal for a penalty fee or a revised dynamic rate multiplier based on the Analyzer's data.
4.  **Validation & Safety Agent (Validator):**
    *   **Responsibility:** Checks the Action Agent's output against strict business boundaries. Ensures a proposed surge price does not exceed the legal maximum cap or that an overstay fine aligns with regulatory schema.

### 5.2 Workflow Execution & Human Approval
1.  **Trigger:** A parking session expires in PostgreSQL, or traffic density hits a threshold. ASP.NET Core initiates the Agentic workflow.
2.  **Execution:** LangGraph routes the state through the Planner -> Analyzer -> Action -> Validator loop.
3.  **Human-in-the-Loop:** If the Action Agent proposes a penalty fee or a surge price increase, the workflow **pauses**. 
4.  **Review (React):** An administrator logs into the React web application, reviews the AI execution summary, and clicks "Approve" or "Reject".
5.  **Finalization:** The ASP.NET Core backend finalizes the transaction, writes the audit logs to PostgreSQL, and updates the driver's Flutter app.
