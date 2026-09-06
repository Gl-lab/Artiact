# Bounded deterministic combat progression

Epic 6's first HTTP slice is available through `CombatSessionFactory`. It is registered in DI but is not selected by the default mining worker. Creating a session reads the character and fresh combat catalogs; repeatedly call `CombatRun.ExecuteCycleAsync` until Completed or Blocked. Callers supply `CombatLevelGoal`, monster code and `CombatLimits`; no default live-combat startup or durable recovery is provided.

The factory and port share the same scoped `GameClient` as `IGameClient`. The client retains exact last character/action JSON for presence-aware normalization; do not log these payloads. Combat catalogs bypass legacy coordinate-only cache DTOs, preserving map identity, layer, access and interactions. Mining keeps its existing cache path.

## Supported policy

The initial implementation narrows ADR 0001 to fire-only attacks, normal monsters, no active effects, explicit secondary zero attacks and bounded stats. Missing required combat/inventory fields block instead of becoming zero. Current/target maps require explicit standard access on the same layer with no conditions/transitions.

`CombatPrediction` uses half-up staged rounding, no favorable outgoing critical/order assumption, maximum incoming critical damage if possible, and at most 50 exchanges. Safe means at least one remaining modeled HP at full health. Recovery precedes fight while HP is below maximum; returned HP and cooldown are authoritative.

The pre-owned weapon comparator accepts one `attack_fire` effect and no conditions; it subtracts the current weapon's contribution and evaluates candidate survival loss. It chooses a strictly better safe candidate, with ordinal code ties. Item level only filters eligibility. Unequip/equip are separate commands with exact inventory conservation and returned-slot checks.

Each decision serializes run access, charges counters before dispatch, and invokes at most one action. Explicit runs default to 20 decisions including terminal reporting, 4 fights, 2 rests, and 3 commands without fight XP/level progress. Gear acceptance supplies no-progress 5. Only a successful fight XP/level increase resets that counter. Completion, invalid state, pressure, limits, unsupported access, unsafe/unknown combat, invalid postconditions, rejection/ambiguity and defeat are typed terminal outcomes. Repeated calls return the same terminal decision.

Responses are saved to `CharacterService` before subsequent checks/cancellation. The port checks cooldown fields, movement map identity, rest restoration, equipment transaction identity and fight participant details. Invalid normalized state remains available as the client's raw payload and saved DTO. Defeat does not authorize recovery. Legacy `ActionStep` also stops on loss and retains the response.

## Deterministic scenarios

`CombatScenario.json` is an authored synthetic fixture. A separate lock-protected combat kernel and middleware serve the subset; existing mining controllers/fixtures retain their behavior. Reset to `combat-progression` or `combat-equipment`, then load `researcher` (case-insensitive). Routes include catalogs, map-id movement, fight, rest, weapon unequip/equip and mock state/trace. Rejected actions leave state/time/trace unchanged. Combat state/trace omit generation and report virtual seconds.

| Scenario | Commands | Decisions | Seconds | Final state |
|---|---|---:|---:|---|
| combat-progression | move, fight, rest, fight | 5 | 29 | level2, XP0, HP14, map2, two feathers, free8 |
| combat-equipment | unequip, equip, move, fight, rest, fight | 7 | 35 | same combat state, quick_blade equipped, old and heavy_blade retained, free6 |

`CombatProgressionFlowTests` uses real clients, empty in-memory cache, TestServer, the session factory and a no-wait cooldown. Both scenarios replay exactly. This is scripted acceptance, not an upstream combat emulator.

```text
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~Combat
dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~CombatProgressionFlowTests
```

## Remaining scope

Loot/craft prerequisites, broader adversarial HTTP response coverage and final Epic 6 acceptance remain open. No live character run, effects/consumables, banking, defeat recovery or restart reconciliation follows from these deterministic results. The strategy portfolio migration belongs to Epic 7 after the remaining craft slice is verified.
