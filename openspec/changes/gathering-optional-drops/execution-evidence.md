# Evidence — 2026-09-07

Base 572b101. Self-review of GatheringStrategy and GatheringDropTests: positive unique drop bounds, long summed capacity, guaranteed output, optional zero delta, preservation of undeclared stock and character fields. Existing action reply validator already conserves all returned items. No DTO, DI, mock or diagram changes needed; existing deterministic flows retain their outputs. No independent review.

Official evidence: GET https://api.artifactsmmo.com/resources?skill=mining returned copper_rocks with copper_ore (rate 1) and five optional entries (rate 200), each quantity 1. GET https://api.artifactsmmo.com/maps?content_code=copper_rocks identified standard overworld map 277 at (2, 0). Current https://api.artifactsmmo.com/openapi.json defines DropRateSchema.rate as integer >=1, chance 1/rate. Maximum-sum capacity is deliberately conservative; simultaneous occurrence is not assumed impossible. No live gathering outcome was tested.

- RED `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~GatheringDropTests`: 7 failures, no supported command for the multi-drop fixture / incorrect pressure reason.
- Same focused command after implementation: 7 passed. Five malformed/rare-only table cases added in review, all passed in the full gate.
- `dotnet test Artiact.sln --no-restore`: 478 application / 160 mock passed, zero failures/skips.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 46 passed.
- `dotnet build Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --warnaserror`: zero warnings/errors.
- `ARTIACT_REAL_API_READONLY=1` with `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --filter Category=RealApiInspect --logger 'console;verbosity=detailed'`: 1 passed. Result Selected/InspectOnly, skill:mining, Move:277, Attempts=0, configured cycle estimate 12 seconds (7 travel + 5 gathering), score 2.5. Environment opt-in removed afterward.

This clears read-only planning for gllab mining target 2 at the observed state. It does not clear OneShot, live gathering reconciliation, long-running execution or container telemetry. The first reviewable action is move to map 277; freshness must be rechecked immediately before any approved dispatch. Production item recipes retain their separate ingredient-source restrictions.
