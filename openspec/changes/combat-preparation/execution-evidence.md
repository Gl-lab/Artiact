# R4c evidence — 2026-09-07

Base 41895f8. Self-review: opt-in preparation gate, current-state combat viability, multiple leaf stock ledger, required-inventory equipment goal, bank-withdraw probe, mock dual drops and unchanged legacy combat scenarios. No independent review.

- RED `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~CombatPreparationObtainsTwoLeavesCraftsEquipsAndCompletes`: Blocked before composition; subsequently passes independent 13-action/81-second/final-stock oracle.
- `dotnet test Artiact.sln --no-restore`: 461 application / 159 mock passed, zero failures/skips. Includes unsafe current weapon (zero dispatch) and shared two-action budget stopping before craft.

No live fight, main host, external state reading, simulator, Docker or telemetry. Configured opponent only; unknown effects, broader equipment and defeat recovery remain outside the supported subset. A drop-free fight may gain XP; total action/decision/time limits, not guaranteed loot, bound such a run.
