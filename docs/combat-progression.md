# Bounded deterministic combat progression

Epic 6's first HTTP slice is available through `CombatSessionFactory`. It is registered in DI but is available separately from default staged portfolio inspection. Creating a session reads the character and fresh combat catalogs; repeatedly call `CombatRun.ExecuteCycleAsync` until Completed or Blocked. Callers supply `CombatLevelGoal`, monster code and `CombatLimits`; no default live-combat startup or durable recovery is provided.

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
| combat-crafting | move, fight, workshop move, craft, unequip, equip, rest, arena move, fight, rest, fight, rest, fight | 14 | 81 | level3, XP0, HP17, map2, crafted_blade equipped, quick_blade1/feather3 retained, free6 |

`CombatProgressionFlowTests` uses real clients, empty in-memory cache, TestServer, the session factory and a no-wait cooldown. Both scenarios replay exactly. This is scripted acceptance, not an upstream combat emulator.

```text
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~Combat
dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~CombatProgressionFlowTests
```

## Loot and craft prerequisites

`CombatSessionFactory.CreateCraftingAsync` accepts an explicit craft target code. CombatCraftPlanner reuses WearCraftTargetFinder, TargetLootingResolver and CraftChainBuilder through a current-map adapter restricted to supported access/layers. It admits a supported improved weapon, one missing mob leaf from the selected viable opponent, sufficient skill and reachable workshop. Immutable commands preserve exact batches, ingredients, output and workshop IDs. Invalid/unsupported plans block before mutation. Completed/invalid goals do not require catalog loading.

The run fights only for missing planned leaf ingredients, then moves/crafts one command at a time, checks exact inventory subtraction/output and advances its craft index only after a valid response. Crafting that consumes sufficient stock can proceed at full capacity if its output fits; a pending equip may likewise free inventory space. The fixture uses limits30 decisions/6 fights/4 rests/7 no-progress commands. Recipe preflight rejects zero/negative yields and overflowing batches before recursive planning.

Combat-crafting starts with quick_blade, obtains one feather, consumes it into crafted_blade at map3, equips it and reaches level3. Craft awards one synthetic weaponcrafting XP. Complete independently authored action oracles and state/trace/decision replay cover all three combat fixtures. HTTP corruption tests cover missing stats, wrong movement/opponent/participant HP, invalid drops/rest/equipment/craft inventory, lost response and cancellation; unsupported workshop, skill and recipe cases emit no actions.

## Remaining scope

No live character run, effects/consumables, banking, defeat recovery or restart reconciliation follows from these deterministic results. Craft factory scope requires one missing leaf; it is not a generic crafting objective or automatic inventory cleanup policy. Failed craft-plan construction currently uses the general UnsupportedAccess terminal reason. The configurable strategy portfolio and an additional profession belong to Epic 7.
