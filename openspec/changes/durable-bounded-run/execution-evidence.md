# R2 evidence — 2026-09-07

Base a949e5c (R1 pushed to origin/codex/autonomous-roadmap). Self-reviewed diff: checkpoint/journal, StrategySession restore/write boundaries, bounded staged loop/settings/status, tests and affected documentation. Independent review not performed.

- RED: `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~DurableRunTests`: four behavior failures before session persistence integration (intent failure dispatched, restart lost reconciliation and reset budgets). Temporary unused-parameter warnings disappeared after integration.
- Focused: `dotnet test Artiact.sln --no-restore --filter "FullyQualifiedName~DurableRunTests|FullyQualifiedName~StagedOperationTests"`: 9 application and 24 mock cases passed.
- Full: `dotnet test Artiact.sln --no-restore`: 450 application and 146 mock tests passed; zero failures/skips.

Tests cover lost response, failed result save, persisted pre-POST intent with unchanged world, cancelled terminal restart, elapsed downtime, changed identity, file lease exclusion/JSON roundtrip, three-action mock mining completion and zero-action completed restart. Checkpoint failures are injected in-process; actual process kill/power loss and distributed filesystems are not proven. Live API/actions, Docker/telemetry and separate unchanged real-API/experimental projects not run.

Review limitations: one local directory and cooperating Bounded owners; external clients and OneShot/Legacy are outside the lease. New-run archival is operator-managed. These are explicit scope boundaries, not automatic restart guarantees for other modes.
