# R8 evidence — 2026-09-07

Base: `a6e02f4`. Self-reviewed diff: capacity policy/configuration, shared ProductionStock planning, ItemProductionPlan/Strategy, R7 integration, scripted capacity scenarios, behavior tests and guides. No independent review is claimed.

Acceptance: five tools with three inventory units complete in 42 commands and 228 virtual cooldown seconds, preserving protected=1. Competing bar=1/tool=1 goals retain both final quotas in 14 commands/77 seconds. A constrained R7 training scenario completes three tools after reaching weaponcrafting 2. Deposit, withdrawal and craft response loss each reconcile after reconstruction without another POST. A two-unit inventory rejects the minimal recipe before any action; a full bank stops after eight commands without a bank trip. Intermediate bank withdrawal ordering has an independent plan oracle.

Commands:

- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~OrderLargerThanInventory`: RED before planning edits (Blocked instead of Completed); GREEN after implementation (1 passed). Policy shape and the independent mock fixture preceded this behavior RED.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~SharedIngredientGoal|FullyQualifiedName~SkillPreparationAndLarger|FullyQualifiedName~OrderLargerThanInventory"`: 3 passed. The shared-quota test first exposed three unnecessary retrieval commands; reserving bank stock before inventory stock resolved the reproducible 17-versus-14 command result.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~CapacityBankAndCraft|FullyQualifiedName~ImpossibleCapacity"`: 5 passed.
- `dotnet test Artiact.sln --no-restore`: 500 application and 184 mock/process tests passed, no failures/skips.
- `git diff --check`: passed, LF/CRLF notices only.

No external API contract, DTO or route changed. No live API/game actions, containers, separate research suite or remote CI verification were performed. RealApiOffline was last checked at R7 (54 passed), not rerun for this internal planning change. Batches are deliberately one recipe execution and do not claim optimal routing or full feasibility for every temporary-storage puzzle. See docs/capacity-production.md for remaining scope.
