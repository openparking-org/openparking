# Security Policy

## Supported Versions

Security updates and critical patches are actively applied to the following release branches:

| Component / Stack | Supported Version | Status |
|---|---|---|
| ASP.NET Core API | .NET 8.0 LTS | :white_check_mark: Supported |
| Web Dashboard | React 18 / Node 20 LTS | :white_check_mark: Supported |
| Mobile Application | Flutter 3.x / Dart 3.x | :white_check_mark: Supported |
| AI Service | Python 3.11 | :white_check_mark: Supported |
| PostgreSQL | PostgreSQL 16 | :white_check_mark: Supported |

---

## Reporting a Vulnerability

The OpenParking team takes software security seriously. If you discover a security vulnerability or sensitive data leak, please report it responsibly:

1. **Do NOT open a public GitHub issue.**
2. Send an email to the repository maintainers at:
   **security@openparking-org.github.io** or submit an advisory via **GitHub Security Advisories**.
3. Include detailed reproduction steps, payload examples (if applicable), and an assessment of potential impact.

### Response Timeline
- **Initial Acknowledgement:** Within 24 hours.
- **Triage & Assessment:** Within 48 hours.
- **Fix & Patch Deployment:** Critical vulnerabilities are addressed within 7 days.

---

## Security Practices in OpenParking

1. **Secret Scanning & Push Protection:**
   - GitHub secret scanning and push protection are enabled across the entire `openparking-org` organization.
   - Any commit containing private keys, access tokens, or connection strings is blocked automatically at `git push`.

2. **Environment & Key Management:**
   - Secrets (`JWT_SECRET`, `MAPBOX_ACCESS_TOKEN`, `RESEND_API_KEY`, `CF_API_TOKEN`, `ORACLE_VM_SSH_KEY`) are managed exclusively through **GitHub Organization Secrets** and injected via environment variables.
   - Local `.env` and `.env.local` files are strictly excluded via `.gitignore`.

3. **Edge Protection & Gateway Hardening:**
   - All inbound internet traffic routes through **Cloudflare Workers**.
   - Direct port access to the Oracle VM origin is blocked; traffic flows securely over an encrypted **Cloudflare Tunnel**.
   - Inbound requests undergo edge-level CORS verification, rate limiting, and JWT token structure pre-validation.

4. **Automated Dependency Updates:**
   - Dependabot runs weekly scans across NuGet, npm, pip, pub, Docker, and GitHub Actions to automatically patch known vulnerabilities (CVEs).
