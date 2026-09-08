# R20 verification — 2026-09-08

Base and reviewed scope: `b4e1d23` plus the bounded-schedule change: ScheduleRunner/Store/Worker and shared execution adapter, operation registration/endpoints, panel assets, ScheduleTests, OperationRegistrationTests, OperatorHttpTests, StagedOperationTests and linked documentation. Self-review only; no independent agent review. The user's `.serena/project.yml` is excluded.

Specification was written before implementation. Initial compiling RED: `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~ScheduleTests` produced 5 expected failures and 1 pass against the scaffold. Later expanded boundary tests compiled with 2 failures / 14 passes: a profile change during Inspect and an invalid persisted NextDue could permit execution. Both defects were fixed with post-inspection identity comparison and stronger persisted-state validation. One intermediate solution attempt had a missing-required-fields compilation error in the new consent test fixture; it was corrected and is not counted as behavioral RED.

Verification commands:

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~Schedule|FullyQualifiedName~OperationRegistrationTests"`: 25 passed.
- `dotnet test Artiact.sln --no-restore --filter "FullyQualifiedName~Schedule|FullyQualifiedName~OperatorHttpTests"`: at the initial integration checkpoint, 16 application and 11 mock tests passed. Later added application boundaries are covered by the final focused/solution gates.
- `dotnet test Artiact.sln --no-restore`: final 599 application + 292 mock passed, zero failures/skips; no build warnings reported.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 74 passed, zero failures/skips. Subsequent changes were panel text, documentation and one application shutdown test, with no API-boundary production changes.
- `node --check Artiact/Operator/panel.js`: passed.
- `git -c core.safecrlf=false diff --check`: passed.

Coverage: durable aggregate reservations across new RunIds/restarts; no catch-up; aggregate exhaustion before MaxRuns; interrupted reservation, corrupt/missing files, changed identity and invalid due dates; occupied series/character lease; failed reservation write with zero execution; Unknown/Cancelled/Blocked refusal; disable, stop during execution and hosted worker shutdown; expiry/profile changes during Inspect; monotonic notification IDs and last-100 retention; default worker selection; required observation panel and separate live-series consent. TestServer verifies local same-origin/control-token protection and absence of manual start routes. The shared real-client mock integration executes and archives two bounded runs, preserves total reservation and dispatches no additional actions on restart.

Visual QA: CUA inspected actual HTML/CSS/JS served by a temporary loopback-only Python fixture on port 18724, with synthetic series state and no game client or credentials. Waiting state showed 1/2 runs, 6/12 actions, 40/80 decisions, 300/600 seconds. Clicking Stop then reloading showed terminal Stopped, a disabled button, unchanged budget and exactly two notifications with Russian reasons. Screenshot confirmed the desktop layout. This verifies rendering and browser interaction; HTTP security and actual execution are covered separately by tests. Mobile viewport was not separately exercised.

Review checked reserve-before-dispatch, no refund, atomic state/events, interrupted archive boundary, shared checkpoint lifecycle/ownership, no dispatch from GET, finite interval/expiry, stop/host cancellation, default-off configuration and unchanged API contracts. Guides, architecture diagram and nearest application instructions are updated. Storage/configuration failure stops the worker; durable notification delivery cannot be promised when the store itself fails.

Not verified: live scheduled game actions, external notification delivery, distributed ownership, arbitrary power-loss/filesystem durability, 24/7 operation and the historical R16 transient cause. R16 investigation was explicitly waived by the user; it is not claimed fixed. The earlier single R19 approval is not series consent. No live schedule was enabled and the completed gllab run was not replayed.
