# R12 verification — 2026-09-08

Base/review identity: 4baaf29 plus the R12 working diff in Operation, Strategy, scripted discovery mock scenarios, AutonomousExecutionFlowTests, RunProcessRecoveryTests, lifecycle/discovery unit tests and associated documentation. Self-review only; no independent agent was requested. User .serena/project.yml remains excluded.

RED evidence: Bounded registration failed with AutonomousGoalsRequireInspect before enabling R12. New mock reset failed 404 before adding discovery scenarios. Unsafe autonomous archive accepted a final state lacking observed skill caps; the test exposed it and archive now validates actual cap facts. Elapsed-budget-after-POST returned Selected before the deadline check; it now stops without starting a cooldown wait. Deadline cancellation preserves verified facts with incomplete wait coverage and distinguishes explicit user cancellation.

Commands/results:

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~AutonomousGoalDiscoveryTests|FullyQualifiedName~RunLifecycleTests"`: 33 passed on final code, zero failed/skipped.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~AutonomousExecutionFlowTests`: 19 passed before adding three checkpoint-version diagnostics; final solution includes those additional cases.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~AutonomousExecutionFlowTests|FullyQualifiedName~RunProcessRecoveryTests"`: earlier 21 passed, including all six manual/autonomous process-kill cases.
- `dotnet test Artiact.sln --no-restore`: final code, 543 application + 242 mock tests passed, zero failed/skipped; includes the complete comparison/replay matrix and six process-kill recovery cases.
- `git diff --check`: no whitespace errors; LF/CRLF normalization warnings only.

Failures retained: the original uneven lowest-rule prediction was wrong; Amendment 1 in comparisons.md records the observed outcome and the full repeat without changing fixtures/formula/ceilings. One full run had 542 application passes and 241 mock passes / 1 failure: a zero-action terminal reopen returned CheckpointUnavailableOrInvalid. Its isolated two-case command passed; added a store-exception/time/state diagnostic, and the subsequent full run passed 542 + 242. The transient was not reproduced and its underlying cause is unproven; do not attribute it to a particular filesystem or clock issue. A later deadline fix adds one application test and requires a final full rerun.

Review covered stable manual identities/numeric statuses, version-2 restore, active-world changes, suppression, bounded history, budgets across target changes, original pending baseline reconstruction, write failures before selection/after completion, exact archive bytes, zero-action caps, RunId reuse with changed policy/limits, actual cap validation and measured facts. Existing architecture flow still uses the same session/client/journal; no structural diagram change required. Process tests use sentinel credentials and loopback-only TestServer proxy.

Not verified: live API/actions, RealApiOffline (no shared DTO or wire route changes), healing/combat discovery, broader R15 matrix. Scripted XP is not the live formula. This is local noncombat MVP evidence, not approval for production gameplay.
