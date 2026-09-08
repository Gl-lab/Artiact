# R22 evidence — 2026-09-08

Base `e4cf63a`; self-reviewed this commit's discovery, version validation, operator evidence, tests and docs diff. R25.1 design preceded implementation. Checked nested instructions and autonomous documentation; manual identity remains unchanged. No independent review claimed.

- RED: `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~UnreachableUnlockFallsBackOnlyToFittingNearestLevel`: 2 failed because no target-2 alternative existed.
- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~AutonomousGoalDiscoveryTests`: 25 passed (capacity boundary, simultaneous drops, budget fallback, no duplicates and active evidence round-trip).
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~IntermediateCompletesAcrossRestartsWithoutReturningBudget`: 1 passed; three real-client/TestServer actions, session recreated each tick, original-target history retained and terminal reopen adds no POST.
- `dotnet test Artiact.sln --no-restore`: 623 application + 303 MockService tests passed on final code, including old-algorithm execution rejection.
- `node --test Artiact.Tests/operator-panel.test.cjs`: 17 passed.
- `git diff --check`: passed; only LF/CRLF normalization notices.

Not verified: live API/action compatibility and R25 needs execution. Future XP remains an estimate; fallback does not guarantee real completion. R23 must still prove banking path feasibility and cost.
