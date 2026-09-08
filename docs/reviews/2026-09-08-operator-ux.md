# Operator UX verification — 2026-09-08

Scope: local diff from `98f95e3b73064553192b77ea3f8efd36ce2610eb` in `Artiact/Operator/panel.{html,css,js}`, `Artiact.Tests/operator-panel.test.cjs`, and the operator/development guides. Existing `.serena/project.yml` changes are unrelated and untouched.

Acceptance: one explicit Start performs fresh Inspect then Start; rejected preparation never dispatches Start; repeated clicks cannot duplicate pending preparation; preview remains optional and read-only; controls explain their limits and auxiliary actions; waiting is visible immediately. Unchanged lists are retained, snapshot polling is every two seconds, and commands request an immediate snapshot refresh. The server's two-second snapshot cache and API latency still apply. Backend permissions, receipt expiry, ownership and archive guards are unchanged.

Verification commands:

- `node --test Artiact.Tests/operator-panel.test.cjs`: first three tests failed against the original script on missing preparation, unintended Start after rejection, and missing duplicate suppression (behavioral RED). Final six tests pass, including preview followed by fresh preparation, invalid limits and transport failure without retries.
- `node --check Artiact/Operator/panel.js`: passed.
- `dotnet test Artiact.sln --no-restore --filter FullyQualifiedName~Operator`: 16 application and 12 HTTP tests passed.
- `dotnet test Artiact.sln --no-restore`: 599 application and 294 mock/compatibility tests passed, no skips or failures.
- `git diff --check`: passed.

Browser verification: actual embedded HTML/CSS/JS served by a temporary loopback-only static fixture with synthetic GET responses, with no application host or game API. Main form and expanded advanced section verified in the in-app browser; screenshot inspected for layout and legibility. Idle Start is enabled, Stop/continuation/archive are disabled, advanced controls are initially collapsed. Script behavior is additionally exercised by the Node tests with simulated DOM and HTTP boundaries.

Review: self-review of the scoped local diff, including refusal paths, profile-bound receipts, duplicate suppression, editable settings during preparation, archive conditions, and preserved textContent rendering. No cross-project contracts or diagrams changed. Browser command execution against the real API, mobile viewport QA, and live latency profiling were not performed; this change does not claim to accelerate the game API or its cooldowns. Tests do not run automatically under the .NET gate; the separate Node command is documented in development.md.

Follow-up copy review: the badge now says «Один персонаж», the header says «панель управления», and the footer specifies direct access from the machine running Artiact. Verified against `OperatorEndpoints.IsLocal`: both the peer address and Host must be loopback. This is not a claim that SSH tunnels or local reverse proxies cannot forward access. No access-control behavior changed.

## Trusted-network server follow-up

Supersedes the preceding loopback copy decision: the user explicitly requested server access within an isolated network without authentication. Removed only the `IsLocal` guard from `/operator` routes. Retained same-origin/CSRF POST checks, server execution opt-ins, receipt expiry, archive rules and the legacy `/operation/stop` loopback guard. Removed the now-invalid UI access statement. Updated current operator/development/limitations documentation, application instructions and the existing R18 proposal/design/acceptance. Listener and firewall settings were not changed.

Reviewed the local diff from the same base above in `OperatorEndpoints.cs`, `OperatorHttpTests.cs`, `panel.html` and the associated documentation. No independent review was requested. New HTTP cases first produced eight behavioral failures because LAN requests received 403; after the change all 17 HTTP cases passed. Coverage includes server IP/hostname HTML/assets/snapshot reads, LAN control/schedule requests, missing-token and foreign-origin rejection, and disabled-panel 404.

- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~OperatorHttpTests`: 17 passed.
- `dotnet test Artiact.sln --no-restore`: 599 application + 301 mock/compatibility tests passed; no failures or skips.
- `node --test Artiact.Tests/operator-panel.test.cjs`: six passed.
- `git diff --check`: no whitespace errors; Git reports its existing LF-to-CRLF normalization notice for OperatorEndpoints.cs.

Unverified: actual deployment, network routing/firewall reachability and live game API execution. HTTP verification is socket-free TestServer with explicit peer/Host/origin values. The existing UI changes and unrelated `.serena/project.yml` work remain uncommitted.
