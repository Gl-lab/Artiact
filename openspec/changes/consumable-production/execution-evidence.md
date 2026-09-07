# R8a evidence — 2026-09-07

Base: `cc4b4bd`. Self-reviewed diff: consumable policy/wrapper/port, charged journal/run context, pending-candidate reconstruction, profession schema/training extension, no-progress setting, mock scenarios/tests and guides. No independent review is claimed.

## Verification

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~ResourceChargesSurvive`: intended RED before budget enforcement (second dispatch instead of ResourceBudgetExhausted). Data shape/constructor metadata were introduced first; the temporary unused-parameter warning was removed by implementation. Focused DurableRunTests subsequently passed 12 tests.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~SharedCommandRestores`: RED exposed ambiguous parent reconstruction (Blocked instead of Reconciled), then 1 passed after persisting selected candidate and using EvaluateAll. Existing older ambiguous checkpoints remain fail-closed.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~HealingSupplyUse|FullyQualifiedName~FoodSupplyTrains"`: 4 passed. These scenario/use tests were added with implementation; no test-first claim beyond the journal cases above.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~LostUseReply|FullyQualifiedName~FullHpAndCompleted|FullyQualifiedName~SupplyCannotReset|FullyQualifiedName~OptionalRestUses"`: 5 passed.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~UnsupportedOrUnverifiedUse|FullyQualifiedName~ConsumableCannotSpend"`: 4 passed.
- `dotnet test Artiact.sln --no-restore`: 501 application and 198 mock/process tests passed, no failures/skips.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 54 passed.
- `dotnet build Artiact.sln --no-restore`: succeeded, zero warnings/errors.
- `git diff --check`: passed, LF/CRLF notices only.

Literal scenario oracles verify 23/127 production, 25/137 constrained-capacity and 11/51 bank-stock action/seconds totals, final HP20, remaining meal=2, use charge=2 and material charge=4/4/0. Main supply acceptance reconstructs every tick. Training acquires cooking/fishing requirements; failed/unknown use does not replay. Full HP/completed parent, reserves, no-progress/material limits, returned item/HP validation and optional rest are covered.

Public unauthenticated reads checked the use request/response schema, cooked-gudgeon recipe/effect and cooking/fishing progress fields, plus official rest/use rules. No authenticated/live game actions, container acceptance, combat research suite or remote CI evidence. DTOs and IGameClient were unchanged: the existing UseItem client is reused with raw envelope validation. Automatic combat consumption and timed effects remain excluded.
