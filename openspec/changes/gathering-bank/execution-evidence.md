# R3 evidence — 2026-09-07

Base 7b69fe6, published R2. Self-review covers GameClient/IGameClient additions, bank policy, fingerprints/durable baselines, prerequisite, exact response conservation, mock atomic bank commit and docs. No independent review.

- RED: `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~GatheringBankConservesProtectedStockAcrossTwoDeposits`: Blocked before prerequisite implementation. Same test subsequently passed with independent 13-action/71-second oracle.
- RED: `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~IncompleteBankPagesBlockBeforeAction`: unexpectedly Selected before bank pagination completeness validation; corrected.
- Focused: `dotnet test Artiact.sln --no-restore --filter "FullyQualifiedName~StagedOperationTests|FullyQualifiedName~DurableRunTests"`: 9 application / 29 mock tests passed before final pagination test.
- `dotnet build Artiact.sln --no-restore`: successful, zero warnings/errors.
- `dotnet test Artiact.sln --no-restore`: 450 application / 152 mock passed, zero failed/skipped.
- `dotnet restore Artiact.RealApiTests/Artiact.RealApiTests.csproj` and `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: successful, 36 passed.

Official sources and subset are recorded in docs/gathering-bank.md. No authenticated read, live game action, production host, Docker or telemetry verification. Old pending checkpoints whose observation fingerprint predates bank inclusion fail closed; no migration/retry is inferred. R2's local ownership limits remain.
