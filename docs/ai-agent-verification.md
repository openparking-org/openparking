# AI agent verification — 4 October 2026

Docker API, AI, PostgreSQL and Redis were running. The AI service was in `live` mode. Model calls below used the configured Cloudflare account rather than mock responses.

| Capability | Result |
| --- | --- |
| Cloudflare model | Direct inference passed. The original gateway URL was malformed; the hardcoded gateway also was not configured in Cloudflare. Corrected native adapter configuration, made gateway opt-in and enabled JSON response mode. |
| Analyzer / pricing workflow | Live request for 95 occupied bays out of 100 produced a structured analysis and a proposed rate of 7.50 from base rate 5.00. Workflow paused at `PENDING_APPROVAL`. |
| Planner resume | Resuming that Python pricing workflow completed the remaining orchestration step. Python's apply step is a placeholder: this does not prove a price was written to the application database. |
| Overstay / Action agent | Failed during ordinance retrieval. Cloudflare AI Search returned HTTP 401, code 10000, Authentication error. The configured model token works for inference but does not currently authorize this Search request. No real overstay penalty was approved or applied. |
| Permit Validator | Through the authenticated .NET proxy, a future permit passed field validation and an expired permit failed. This is metadata validation; the supplied document image is not read or authenticated. |
| Cartographer | The authenticated proxy returned four generated bays for a two-by-two grid. No YOLO model is loaded or executed. Corrected the misleading algorithm label and random confidence claims, and labeled the admin button `Generate grid preview`. |
| Routing | A* returned the expected A-to-B path and distance 50 for coordinates (0,0) and (30,40). |

## Fixes and regression checks

- Native Cloudflare model endpoint with optional `CF_AI_GATEWAY`, deterministic temperature and JSON response format; sufficient output token budget avoids truncated analysis.
- Python settings tool now reads the .NET `data.value` response envelope instead of silently using the default value.
- Added separate optional `CF_AI_SEARCH_TOKEN` and configurable `CF_AI_SEARCH_INSTANCE`. Docker forwards these settings. Tokens were not printed or saved in this report.
- Grid previews report `Geometric-grid-preview` and zero model confidence rather than claiming a trained vision-model result. The admin screen explains that positions and bay types require review.
- AI regression suite: 31 tests passed in an isolated mock-mode test process. The running service remained in live mode.
- Web TypeScript, lint and production build passed after the initial label changes; the final removal of the confidence display was also type-checked and linted.

## Remaining work

1. Configure an authorized Cloudflare AI Search token and confirm the instance exists and contains the parking ordinances. Set `CF_AI_SEARCH_TOKEN` and, if needed, `CF_AI_SEARCH_INSTANCE` in the existing local environment configuration, then recreate the AI container. Real overstay proposal testing can then continue.
2. A real .NET proposal-to-approval-to-database test is still required. The attempted controlled retry fixture could not reach the API because the API's configured database differs from Docker's local PostgreSQL database. The local fixture and temporary API-created zone were removed; existing records were preserved.
3. Pricing workflows currently have no normal admin-panel trigger other than retrying a failed run. Direct Python pricing success does not establish automatic operational scheduling or integration with real arrival history.
4. Genuine image-based bay detection and permit OCR/authenticity checks require implementations; these are not present in the current agents.
5. Python workflow persistence falls back to process memory because its internal write endpoints are not implemented by .NET. The .NET-triggered workflow stores its final proposal in the application database, but direct Python resume state is not durable across container restarts.

No payment processing was added. Approval in the direct Python test only exercised its placeholder execution step and did not change a parking-zone rate or issue a customer penalty.
