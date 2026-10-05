# Permit image validation change report

Date: 5 October 2026

## Result

OpenParking now reads uploaded permit images through Gemini, compares the extracted
fields with the driver's submission, and presents the result for administrator review.
Every submission remains Pending until an administrator makes the actual decision.
The development API and AI containers were rebuilt and started after Docker became
available. Both services passed health checks, and the running AI permit endpoint
successfully read the synthetic image with HTTP 200. The API reported its database
connected and all application modules healthy.

## Behavior changes

| Area | Before | After |
| --- | --- | --- |
| Document reading | Image passed through without being read | Gemini extracts number, expiry date and issuer |
| Validation | Supplied metadata checked | Metadata, extracted fields, mismatches, expiry and readability checked |
| Approval | Metadata confidence could auto-approve | Administrator decision required for every permit |
| Review screen | Metadata recommendation and percentage | Uploaded image, extracted fields, readability and specific issues |
| Persistence | Admin results only in browser state | Completed readings stored in the existing audit table |
| Repeated reads | AI could be called repeatedly | Saved completed readings reused |
| Unavailable service | Manual fallback | Explicit unavailable result; manual review or retry available |

## Flow

1. The existing driver form uploads a permit image and entered fields.
2. .NET saves a Pending permit and requests image reading with the internal service token.
3. Gemini reads the image without being supplied the driver's field values as answers.
4. The Validator compares those fields with the submission and applies deterministic checks.
5. Completed readings are saved and shown in the admin permit list.
6. The administrator inspects the document and verifies or rejects it through the
   existing protected review endpoint. That decision changes the stored permit status
   and accessible-parking privileges.

Failures do not reject the submission or grant privileges: it remains Pending.
The main flow uses the real .NET review endpoint. The direct Python planner's legacy
apply step remains a placeholder rather than an application database update.

## Files changed

| File | Change |
| --- | --- |
| `ai/tools/permit_vision.py` | New Gemini adapter, response schema, image validation, timeout, sanitized errors and token usage |
| `ai/agents/validator.py` | Extracted-field comparison, expiry/readability checks, manual-review result; handles naive input dates |
| `ai/agents/planner.py` | Calls document validation and always pauses permit workflows for review |
| `ai/main.py` | Image validation endpoint and authentication for permit reading calls/workflows |
| `api/OpenParking.Core/Interfaces/IUserService.cs` | Adds document-validation method |
| `api/OpenParking.Infrastructure/Services/UserService.cs` | Reads on submission; removes confidence-only auto-approval; saves/reuses readings and supports retry |
| `api/OpenParking.Api/Controllers/AdminController.cs` | Adds reading by permit ID and saved readings in list responses; forwards credentials in the existing proxy |
| `web/src/modules/user-access/PermitReviewPage.tsx` | Image preview, extracted fields, issue messages and saved-reading state |
| `web/src/modules/user-access/SettingsPage.tsx` | Hides obsolete permit auto-approval threshold |
| `.env.example` | Documents separate permit model setting |
| Both Compose files | Pass permit model setting to AI container |
| `ai/tests/unit/test_permit_vision.py` | Extraction, mismatches, expiry, invalid images, failures, authentication and planner tests |
| `api/OpenParking.Tests/PermitVisionTests.cs` | Pending status, caching, retry and authenticated HTTP review tests |
| `tools/verify-permit-vision.py` | Repeatable live synthetic-image smoke test |
| `tools/fixtures/synthetic-permit.png` | Clearly marked synthetic image with no customer information |
| `ai/README.md` | Behavior, configuration and smoke-test instructions |

## API and persistence

- `POST /ai/permits/validate` keeps the existing payload fields and now requires
  `X-Internal-Token` matching the shared server token.
- `POST /api/admin/permits/{id}/validation` loads the stored permit; drivers cannot
  access this endpoint. A completed reading is returned from the cache when available.
- `GET /api/admin/permits` adds `aiValidation` to rows with saved readings.
- Results include document status, extracted fields, issues, reason, metadata validity,
  model, token usage, human-review requirement and unverified authenticity.
- `valid` means the implemented document checks passed, not that the permit is approved.
  The compatibility confidence field is an evidence-completeness indicator, not an
  authenticity probability; the review UI no longer shows it as an AI percentage.
- Completed extraction results, including mismatches/unreadable fields, are saved.
  Infrastructure failures are not cached so configuration fixes can be retried.
- Records use AuditLogs action `PERMIT_DOCUMENT_VALIDATION`. No migration or new
  package dependency is needed. Existing approved/rejected records are not changed.

## Model and cost

The model defaults to `gemini-3.5-flash-lite`, independently of the pricing model.
The live synthetic test correctly extracted `DEMO-12345`, `2099-12-31` and
`Demo Transport Authority`, while still requiring human approval.

It consumed **1,187 input tokens and 59 output tokens**. At $0.30/million input and
$2.50/million output tokens, that call costs approximately **$0.0005036**; 1,000
calls with identical usage would cost about **$0.50**. Actual images and retries vary.
[Google pricing](https://ai.google.dev/gemini-api/docs/pricing#gemini-3.5-flash-lite).

Images are sent server-to-server to Gemini. Existing private key and internal-token
values were present and were neither printed nor changed.

```dotenv
GEMINI_API_KEY=<existing-private-key>
GEMINI_PERMIT_MODEL=gemini-3.5-flash-lite
INTERNAL_API_TOKEN=<same-secret-on-api-and-ai>
```

The model setting can be omitted because the default is in the code and Compose files.

## Verification

| Check | Result |
| --- | --- |
| Full Python AI suite | 85 tests passed |
| Full .NET suite | 103 tests passed |
| Web TypeScript/production build | Passed |
| Web ESLint | Passed |
| Ruff on extraction, its tests and modified validator | Passed |
| Live Gemini synthetic image reading | Passed |
| Rebuilt AI container permit endpoint | HTTP 200; correct extraction; admin approval required |
| Rebuilt API/AI health | Healthy; API database connected |
| Authenticated HTTP review | Passed: driver denied, saved reading listed/reused, admin decision persisted |
| Browser visual verification | Unavailable: no connected browser |

Existing Python/LangGraph deprecation and SignalR bundle annotation warnings did not
fail tests or builds.

## Limits

- Uploaded JPEG/PNG images up to 5 MB are supported. PDF/HEIC are not implemented.
- Arbitrary external image URLs are not fetched. Older URL-only documents require
  manual review, avoiding server-side fetching of user-provided addresses.
- Comparison normalizes case and whitespace. Different issuer abbreviations,
  translations or spelling may still be flagged for review.
- Missing or ambiguous dates/fields are flagged rather than invented.
- No official issuer registry or forgery detector was added. Image reading does
  not prove authenticity or automatically determine eligibility.
- Cache reuse applies to the immutable permit record. Simultaneous first reads
  may still produce multiple calls; there is no distributed exactly-once billing guarantee.

## Repeat checks and deployment

From the repository root, using an environment with the AI dependencies:

```powershell
python -m pytest ai/tests -q
dotnet test api/OpenParking.Tests/OpenParking.Tests.csproj --no-restore
python tools/verify-permit-vision.py tools/fixtures/synthetic-permit.png
```

Run `npm.cmd run build` and `npm.cmd run lint` inside `web` on Windows. The live
smoke test uses the root `.env` and incurs an API charge. Add
`--endpoint http://localhost:8000/ai/permits/validate` to test the running AI endpoint.

Development API/AI containers are rebuilt with `docker compose up -d --build api ai`.
Production Compose references registry images, so publish updated images before
recreating those containers. The updated web build is in `web/dist` for the normal
frontend deployment process.
