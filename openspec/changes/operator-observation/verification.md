# R17 verification — 2026-09-08

Base/review: `47d7f5f` plus operator read API/assets, timestamp and worker-lifetime changes, tests and documentation. Self-review only. R16 publication evidence is included; the user's `.serena/project.yml` is excluded.

RED: after correcting a missing required Username in the test harness, `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~OperatorSnapshotTests` compiled and failed 3 expected projection assertions against the scaffold. `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~OperatorHttpTests` compiled with 3 failures (404 instead of allowed 200 or denied 403), and the disabled-route case passed.

Final focused results: OperatorSnapshotTests 8 passed; OperatorHttpTests 4 passed. Coverage includes lease-held reads and writes, cache expiry/corruption, secret-field exclusion, freshness, worker/host distinction, terminal categories, local-address/Host checks and disabled routes. A final acceptance assertion for unpersisted worker failure was green after the review fix; it is not claimed as a separate RED.

`node --check Artiact/Operator/panel.js`: passed. `dotnet test Artiact.sln --no-restore` on the final code/assets: **568 application + 283 mock passed**, zero failures/skips. `git -c core.safecrlf=false diff --check`: passed.

Visual QA: CUA opened a loopback-only Python preview serving the actual panel assets and a synthetic 12-action terminal snapshot, with no game client or credentials. Screenshot/DOM confirmed the layout, Russian terminal reason, stale timestamp, budget, character and milestone fields. The initial generic browser selection timed out; the in-app browser succeeded. This is visual verification, not execution/live evidence. Responsive CSS is supplied but a separate mobile viewport was not exercised.

Review verified the shared character-key algorithm remains identical, readers use delete sharing, no read endpoint creates a directory/lease or dispatches actions, DTO extension is optional, resource/decision facts remain journal-grounded, and failed storage cannot display cached success after expiry. Updated architecture diagram, application instructions and configuration guide match the implementation.

Not verified: live API, R19 operator cycle, remote/multi-user security, mobile visual layout and R16's historical transient cause. The user explicitly authorized proceeding with R17/R18 while R16 remains open.
