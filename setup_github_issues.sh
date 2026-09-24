#!/bin/bash
# setup_github_issues.sh
# Requires GitHub CLI (gh) installed and authenticated.
# Run this from the root of your repository to automatically create the project backlog.

echo "Creating GitHub Labels..."
gh label create "backend" --color "0075ca" --description "ASP.NET Core API" || true
gh label create "frontend" --color "5319e7" --description "React Dashboard" || true
gh label create "mobile" --color "0052cc" --description "Flutter App" || true
gh label create "ai" --color "b60205" --description "Python LangGraph AI" || true
gh label create "devops" --color "c2e0c6" --description "Docker, CI/CD, Deployment" || true

echo "Creating Issues for Yowun (User Access)..."
gh issue create --title "Yowun: Scaffold Users & JWT Auth API" --body "Implement the Users table and JWT authentication endpoints in ASP.NET Core." --label "backend"
gh issue create --title "Yowun: Disability Permit Schema & Endpoints" --body "Implement the disability_permits table and upload endpoints." --label "backend"
gh issue create --title "Yowun: System Settings API & In-Memory Cache" --body "Implement ISettingsService and the system_settings database table." --label "backend"
gh issue create --title "Yowun: Permit Upload UI (Flutter)" --body "Implement the device camera/file picker to upload permits to Cloudflare R2." --label "mobile"
gh issue create --title "Yowun: Validator Agent (Python)" --body "Implement OCR extraction via CF Workers AI and schema validation for permits." --label "ai"

echo "Creating Issues for Supun (Space & Availability)..."
gh issue create --title "Supun: Zones & Slots Schema" --body "Implement the zones, parking_slots, and floor_plans tables and basic CRUD APIs." --label "backend"
gh issue create --title "Supun: Real-time Occupancy API (SignalR)" --body "Implement the SlotHub and push notifications for slot status changes." --label "backend"
gh issue create --title "Supun: React-Konva Floor Plan Editor" --body "Implement the 2D canvas editor in React for admins to draw slots and waypoints." --label "frontend"
gh issue create --title "Supun: Indoor Route Map (Flutter)" --body "Implement InteractiveViewer and CustomPainter to draw A* routes on floor plan images." --label "mobile"
gh issue create --title "Supun: Analyzer Agent & A* Routing (Python)" --body "Implement the A* algorithm and the Analyzer Agent for occupancy queries." --label "ai"

echo "Creating Issues for Dev (Booking & Payment)..."
gh issue create --title "Dev: Bookings Schema & Endpoints" --body "Implement the bookings table and reservation logic." --label "backend"
gh issue create --title "Dev: Session Lifecycle & Fee Calculation" --body "Implement parking_sessions check-in/out endpoints and the FeeCalculationService." --label "backend"
gh issue create --title "Dev: FCM Push Notifications Integration" --body "Implement Firebase Cloud Messaging integration in ASP.NET Core and Flutter." --label "backend,mobile"
gh issue create --title "Dev: Booking Flow UI (Flutter)" --body "Implement lot search, availability calendar, and reservation confirmation screens." --label "mobile"
gh issue create --title "Dev: Action Agent (Python)" --body "Implement the AI agent responsible for formulating surge pricing and penalty actions." --label "ai"

echo "Creating Issues for Karuna (Enforcement & AI Orchestration)..."
gh issue create --title "Karuna: AI Workflow Runs & Overstay Schema" --body "Implement tables to track agentic workflows and active zone pricing rules." --label "backend"
gh issue create --title "Karuna: AI Workflow Approval Endpoints" --body "Implement APIs for admins to approve or reject AI-proposed actions." --label "backend"
gh issue create --title "Karuna: Admin Analytics Dashboard (React)" --body "Implement Recharts-based dashboard for revenue, occupancy, and AI metrics." --label "frontend"
gh issue create --title "Karuna: QR Code Scanning Workflow (Flutter)" --body "Implement the camera QR scanner device feature for check-in/out." --label "mobile"
gh issue create --title "Karuna: Planner Agent Orchestration (Python)" --body "Implement the master Planner Agent that delegates to other tools and agents." --label "ai"

echo "Creating DevOps & Infrastructure Issues..."
gh issue create --title "DevOps: Setup Docker Compose Environment" --body "Create docker-compose.yml for Postgres, Redis, and APIs." --label "devops"
gh issue create --title "DevOps: Configure GitHub Actions CI Pipelines" --body "Implement CI checks for .NET, Flutter, React, and Python." --label "devops"

echo "Done! Check your GitHub Issues tab."
