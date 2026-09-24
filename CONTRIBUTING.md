# Contributing to OpenParking

Thank you for contributing to OpenParking! This guide establishes our branching strategy, pull request protocol, code standards, and review workflows for all 4 vertical slice contributors.

---

## 1. Branch Strategy

We follow a structured Git branching model to maintain stability across integration phases:

```
feature/slice-name ──► develop (Integration) ──► main (Production Demo)
```

- **`main`**: Protected branch. Only deployable, reviewed code that passes all CI checks. Direct pushes are blocked.
- **`develop`**: Active integration branch where features are merged and tested collectively.
- **Feature Branches**: Branch off `develop` using the following naming conventions:
  - `feature/user-<task-name>` (Yowun)
  - `feature/space-<task-name>` (Supun)
  - `feature/booking-<task-name>` (Dev)
  - `feature/enforcement-<task-name>` (Karuna)
  - `fix/<issue-number>-<description>` (Bug fixes)

---

## 2. Contribution Workflow

1. **Pick an Assigned Issue:**
   - Locate your assigned task on the [GitHub Projects Board](https://github.com/orgs/openparking-org/projects/2).
   - Move the card to **In Progress**.

2. **Create Your Branch:**
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/user-jwt-auth
   ```

3. **Make Commits Following Conventional Commits:**
   Use descriptive, conventional commit messages:
   - `feat(api): add JWT authentication endpoints`
   - `feat(web): build floor plan blueprint editor component`
   - `feat(mobile): add QR scanner for check-in barrier`
   - `feat(ai): integrate LangGraph planner workflow`
   - `test(api): add unit tests for fee calculation`
   - `fix(ai): enforce overstay penalty cap validation`

4. **Verify Locally Before Pushing:**
   Run the tests and linters for your component:
   - **Backend & AI:**
     ```bash
     make test
     ```
   - **Web Dashboard:**
     ```bash
     cd web && npm run lint && npm run type-check && npm run test
     ```
   - **Mobile App:**
     ```bash
     cd mobile && flutter analyze && flutter test
     ```

5. **Open a Pull Request:**
   - Push your branch: `git push origin feature/your-branch-name`.
   - Open a PR against the **`develop`** branch.
   - Complete the checklist provided in the PR template (`.github/pull_request_template.md`).
   - Link the relevant issue (e.g. `Closes #12`).

---

## 3. Code Review & CODEOWNERS

- Pull requests automatically request reviews from component owners defined in [`.github/CODEOWNERS`](.github/CODEOWNERS).
- At least **one approving review** is required before merging.
- All automated GitHub Actions CI checks (API, Web, Mobile, AI) must pass.
- Maintain a clean commit history. Rebase or squash merge as appropriate.

---

## 4. Academic Integrity & Viva Evidence

- **Traceable Commits:** All team members must author and commit their own slice's code from their authenticated GitHub accounts to ensure auditable individual contribution marks.
- **No Hardcoded Secrets:** Never commit `.env` files, production API tokens, or private SSH keys. Use GitHub Org Secrets.
- **Clean Architecture:** Keep components bounded within their respective module folders (`UserAccess`, `SpaceAvailability`, `Booking`, `Enforcement`) and communicate via shared interfaces (`IParkingModule`).
