# R18 verification — 2026-09-08

Reviewed base `ebe20d6` plus coordinator, scoped StagedExecution reuse, lifecycle identity extraction, HTTP guards, panel controls, tests and documentation. Self-review only. R17 publication record included; `.serena/project.yml` excluded.

Compiling REDs: OperatorControlTests initially failed both expected acceptance assertions against the scaffold; ControlPostsRequireSameOriginAndReceiptHeader failed three cases because the controls endpoint did not exist. Review prediction ChangedServerPolicyInvalidatesInspectReceipt then failed (changed Recovery policy was accepted); binding receipts to the full prepared identity/permission/freshness/directory stamp fixed it.

Commands/results:

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~OperatorControlTests`: 7 passed. Duplicate start, expired receipts, policy change, permissions, external lease/corrupt state, busy execution and shutdown covered.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~OperatorHttpTests`: 7 passed. Local/disabled guards and same-origin/CSRF stop acceptance/refusal.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~OperatorCycleUsesRealClientAndPreservesTerminalOnRestart`: 2 passed. The added acceptance was green against the implemented coordinator and existing execution machinery: zero actions in Inspect, duplicate start, completed three-action mining, cancellation while first POST response is held, verified response persistence, terminal reopen without another POST and guarded archive. No new interpretation of those previously tested core guarantees is claimed.
- `dotnet test Artiact.sln --no-restore`: **575 application + 288 mock passed**, zero failures/skips.
- `node --check Artiact/Operator/panel.js` and `git -c core.safecrlf=false diff --check`: passed.

CUA visually checked the actual form assets with a loopback Python synthetic response server: fields/ceiling defaults, disabled start before Inspect, Selected receipt enabling start, accepted response disabling it. This preview contains no application/game transport and is not the integration evidence; the tests above exercise production control and execution. Existing full-process kill tests remain in the solution gate.

Self-review checked DI startup selection, exact old identity serialization, noncombat policy restrictions, receipt expiry/profile binding, no budget reset, shared archive guards, background task cancellation independent of HTTP, same-origin protections and absence of credentials from the projected profile. Architecture, application instructions and panel/development docs updated. The legacy stop route's new loopback restriction is documented.

Unverified: live game/operator acceptance, remote/multi-user deployment, R16 historical transient, and continuous operation/scheduling. R19's existing six-decision protocol conflicts with the runtime's ten-decision minimum; this is explicitly left as a concrete R19 preparation issue, not resolved by increasing live bounds.
