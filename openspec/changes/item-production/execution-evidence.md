# R4a evidence — 2026-09-07

Base 6843216. Self-review: new pure production planner, item-only policy, full-world dispatch around filtered resource planning, workshop/skill/stock checks, withdrawal conservation and durable replay, additive interface and scripted mock. No independent review.

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~ItemProductionTests`: initially one RED new-planner behavior (shared/nested stock); subsequently passed. Cycle/unavailable assertion was already green on rejecting scaffold; not counted as RED evidence.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~ItemGoalProducesNestedRecipeFromGatheredOrBankStock`: two passed, independent stock/action/time oracles (6/32 and 5/25).
- `dotnet test Artiact.sln --no-restore`: 453 application / 156 mock tests passed, zero failure/skips, including lost craft/withdraw restart cases.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 36 passed.

Official OpenAPI withdrawal array and transaction response checked on this date; bank docs were read in R3. Host/live actions, authenticated reads, containers and telemetry not tested. Production eligibility is rechecked per action; this is not a globally optimal inventory/route scheduler. Mob-drop leaves remain outside R4a.
