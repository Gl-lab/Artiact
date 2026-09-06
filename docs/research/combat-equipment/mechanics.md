# Combat mechanics and uncertainty — 2026-09-06

Evidence classes: **documented** is a dated upstream statement; **inspected** is a schema fact; **synthetic** is an experiment rule; **unverified** needs further evidence. No live fight or authenticated simulation was run.

## Combat model

The [combat guide](https://docs.artifactsmmo.com/concepts/stats_and_fights/) documents separate elemental damage stages, half-up rounding, critical hits, initiative ordering and ties, a finite fight limit and defeat relocation. Player/global and elemental bonuses precede resistance; critical amplification follows resistance. It documents a 100-turn cap and defeat at spawn (0,0), HP 1. XP depends on opponent, level and wisdom; effects can alter battle behavior. These are documented mechanics, not measured outcomes.

The prototype implements a deliberately narrower synthetic contract: one active element, positive HP up to 1,000,000, attack 0–10,000, bonuses 0–1,000%, resistance/critical 0–100%, explicit known inputs, no effects, normal opponent only. Decimal arithmetic rounds each stage away from zero. Negative resistance, unknown/missing data, multiple elements, elites/bosses and effects are Unknown. Initiative values are accepted but confer no benefit: the bound charges an opponent attack every exchange. Outgoing critical benefit is ignored; any nonzero incoming critical chance is charged as a critical on every hit. This covers critical/order variability conservatively inside the supported model, without random sampling.

Let `n = ceil(monsterHP / minimumOutgoingDamage)`. Zero outgoing damage is rejected. Reserve two actor turns per exchange and reject n > 50: upstream prose does not unambiguously settle actor-turn versus exchange counting. The loss bound is n times maximumIncomingDamage. Safe requires current HP strictly greater than that bound. Unsafe means insufficient guarantee under the bound, not inevitable defeat. No probability of real-world death is claimed.

## Recovery and action state

The [rest guide](https://docs.artifactsmmo.com/concepts/resting_and_using_items/) describes full HP restoration and a missing-HP-percentage cooldown. The [OpenAPI operation](https://api.artifactsmmo.com/openapi.json) instead describes seconds per five HP. Both mention a minimum of three seconds. **Conflict R1:** the timing formula is unresolved; neither is used for execution or XP/time optimization. Rest responses supply HP, restored amount and cooldown. Fixtures inject cooldown 13 independently; this is not a choice between the formulas.

Research policy rests before fighting whenever current HP is below max, but only after full-health feasibility is established. Partial restoration may repeat within explicit limits; no HP increase, invalid HP or failed recovery stops. Defeat stops immediately after retaining returned state; there is no revenge or automatic recovery fight. Food, utilities and healing effects are excluded. Defeat gold/XP/drop penalties and exact layer after relocation are not proven by the inspected sources; future code must use authoritative returned state, not reconstruct these consequences.

The [action guide](https://docs.artifactsmmo.com/concepts/actions) describes cooldown fields and action lifecycle. The prototype accumulates returned duration without sleeping. Cancellation after a response retains state and stops. An ambiguous response loss stops all mutation with one charged attempt; current production retries remain unchanged. Rejected responses and ambiguous transport outcomes are separate experiment cases.

## Equipment and loot

The [equipment guide](https://docs.artifactsmmo.com/concepts/equipment/) describes named slots, item conditions and array equip/unequip operations. Conditions can depend on stats, possessions, costs or achievements. The experiment permits one pre-owned weapon and an explicitly satisfied condition; it does not implement the full condition language. Failed/unknown conditions, invalid slot and insufficient capacity block. HP-changing gear, consumables, runes and other effects are excluded. Predicted replacement stats are explicit synthetic inputs, never calculated by adding item level. Returned equip/unequip stats supersede them.

The fixed gear comparison minimizes conservative incoming loss against the selected opponent, then ordinal item code; it requires improvement over the current loadout. A lower-level weapon can win that comparison. All candidate stats must already be normalized; a production evaluator needs actual equipped-item accounting and complete effect semantics first.

The schema defines drop rate reciprocally. Research uses scripted authoritative inventory and XP rather than simulating loot rolls, invented XP curves or item consumption. Missing loot therefore cannot drive an unbounded loop. The gold/drop/XP forecast remains outside the viability proof. Finite inventory stops the experiment; no deletion, banking or implicit craft repair is introduced.

## Access and external evidence

The [movement guide](https://docs.artifactsmmo.com/concepts/maps_and_movement/) identifies maps by layer plus coordinates or map ID, places content under interactions and defines access restrictions/conditions. Research map IDs 1 and 2 are synthetic and use an explicit reachable input. It is a scripted precondition, not a pathfinder; wrong returned destination stops. Live path/access validation remains an Epic 6 prerequisite.

The [official simulator guide](https://docs.artifactsmmo.com/members/fight-simulator/) offers member-only solo/group samples with logs. The API additionally names founder access. Neither account entitlement nor the simulation endpoint was tested. No sufficiently matched public outcome corpus was established during this bounded research. Calibration is therefore evaluated as a future evidence channel, with synthetic counterexamples in [comparison](comparison.md), not invented observed fights.

## Blocking scope

R1 (rest timing) blocks predictive time optimization but not response-driven offline control. R2 (complete effects/stat normalization and turn semantics) blocks extending the supported model or claiming live safety. R3 (current DTO/map/transport gaps) blocks real-client combat rollout. R4 (no matched observed/simulator corpus) blocks calibration claims. These findings narrow Epic 6 to deterministic HTTP acceptance first; none prevents completion of this research epic.
