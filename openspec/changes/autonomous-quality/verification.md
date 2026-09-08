# R15 verification — 2026-09-08

Reviewed base `ffa5705` plus R15 comparison/replay tests, two scripted mock aliases and documentation. Production behavior/formula unchanged. Temporary exception instrumentation was removed before final verification. Self-review only; no independent agent requested. User .serena/project.yml remains excluded.

Compiling REDs: new full/no-path resets returned404 before aliases were added. Long auto/fixed costs were42/214 instead of predicted41/207; lowest capacity cooldown was74 instead of68. Amendment1 records why both initial predictions were incorrect. The useful outcomes, fixtures, formula and zero relative regression allowance remain unchanged. All policies and accumulated scenarios were rerun.

Commands/results:

- `dotnet test Artiact.MockService.Tests --no-restore --filter FullyQualifiedName~QualityMatrixLongCapacityAndNoRouteReplay`: 3 passed after the documented prediction amendment, with complete three-policy replay.
- `dotnet test Artiact.MockService.Tests --no-restore --filter FullyQualifiedName~CombatParentLostUse`: 1 passed; retained combat parent and recovery charges, no repeat POST or invented missing-response facts.
- `dotnet test Artiact.MockService.Tests --no-build --no-restore --filter "FullyQualifiedName~RecoveryComparisonsReplay|FullyQualifiedName~CombatDiscoveryComparison|FullyQualifiedName~QualityMatrixLongCapacity" --logger "console;verbosity=detailed"`: 8 passed, emitting full cost/movement/switch metrics twice per policy.
- `dotnet test Artiact.MockService.Tests --no-restore --filter FullyQualifiedName~PreregisteredHttpComparisons --logger "console;verbosity=detailed"`: 4 passed, all R12 metrics/replays retained.
- `dotnet test Artiact.MockService.Tests --no-restore --filter "FullyQualifiedName~RecoveryComparisonsReplay|FullyQualifiedName~CombatDiscoveryComparison" --logger "console;verbosity=detailed"`: 5 passed after adding per-skill snapshot evidence and removing temporary instrumentation.

Retained operational failure: a preceding full solution run passed556 application tests and278 mock tests but failed BoundedMiningCompletesAndRestartDoesNotDispatchAgain (expected Completed, got Blocked). A diagnostic complete mock run and four additional complete mock runs passed279 each. Temporary exception output showed only deliberate incompatible-checkpoint/write-failure injections; the unexpected failure did not reproduce. Its cause and relation to the earlier R12/R13 transient are unproven. Expanded test result diagnostics are retained; passing repetitions are not called a fix.

The final gate covers all36 policy/scenario combinations twice, near-full stock invariants, twenty-parent long stop/reopen and six existing manual/autonomous child-process kill cases. Exact final solution result and staged diff checks are recorded below before publication.

Final `dotnet test Artiact.sln --no-restore`: **556 application +279 mock passed**, zero failed/skipped, after removing temporary instrumentation. `git -c core.safecrlf=false diff --check` passed. PowerShell relative-link checks passed for all15 changed/new Markdown files at the final review. R15 changes no application behavior; the reviewed test/mock/doc diff is relative to `ffa5705`.

Review covered unchanged policy identities/formula, absence of diagnostic production output, guarded bank mock actions, strict initial-state aliases, semantic replay metrics, useful-outcome failures vs early stops, corrected predictions, per-skill counters, stock, durable budgets and missing fact coverage. Existing architecture diagrams still describe the same observer/session/journal; no structural update is needed. Scenario instructions and known limitations match the final code.

Unverified: real API/live actions, matched combat simulator corpus, RealApiOffline (no shared DTO/route changes), scheduler/remote hosting and filesystem power-loss guarantees. The constrained noncombat live protocol is delivered but unexecuted; the combat ADR live no-go remains.

Published implementation/test revision: `845946b`, pushed to origin/master after the final gate. The following documentation-only publication record closes the task checkbox and records all epic revisions; no code or test change follows that tested revision.
