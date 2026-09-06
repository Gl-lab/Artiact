# Epic 6 handoff: deterministic combat and equipment progression

Epic 5 is complete as research. [ADR 0001](../../decisions/0001-combat-viability-and-recovery.md) recommends a bounded offline HTTP implementation first. This document scopes the next OpenSpec; it does not implement it or authorize live fights.

## First acceptance slice

Use the synthetic `researcher` and opponent from [experiments](experiments.md), target combat level 2, map identities 1→2, HP20/attack10 versus HP20/attack3. Golden action responses produce Move, Fight, Rest, Fight, then Completed; five decisions, 29 virtual seconds, final level2/XP0/HP14/free units8/map2. Exact XP awards, inventory values and timings are scenario literals, not assertions about current game balance.

Gear extension: pre-own `quick_blade` and `heavy_blade`, start with `old`; the character-aware comparison selects the lower-level quick blade. Unequip and equip are separate commands, each reconciled. With no-progress limit5 the existing script totals seven decisions, six commands and 35 virtual seconds. Do not assume the old weapon disappears or that a multi-item request is atomic: Epic 6 must author complete inventory/slot/stat response oracles. The prototype carries only free units and a weapon code.

## Ordered implementation prerequisites

1. **Contracts and normalization.** Replace the fight's singular-character assumption with explicit result plus exact-name participant extraction; reject missing/duplicate participants and missing required stats. Represent named equipment slots and array requests; preserve full rest/equipment action details. Migrate affected interfaces/callers/DI/Moq setups together. Preserve mining contracts or provide explicit adapters. Run RealApiOffline for shared DTO changes.
2. **Action transport boundary.** Specify no replay for ambiguous combat/rest/equipment/movement POST failures before enabling these commands. Distinguish known rejection from unknown server outcome; retain returned state and cancellation semantics. Do not reuse GetAction's blind retries as an exactly-once guarantee.
3. **Observations and destination.** Represent map ID/layer/access/interactions. Restrict the initial fixture to explicit standard-access maps with no transitions; block unsupported access instead of assuming a coordinate match is reachable. Version/presence-check normalized combat stats and gear conditions.
4. **Goal and finite transitions.** Implement CombatLevelGoal and the supported ADR policy through the proven application seam, with one command per combat decision. Add validated run limits and typed reasons. Leave nullable legacy LevelUpGoal inert and mining semantics unchanged.
5. **Minimum HTTP mock.** Extend named reset/state/trace fixtures with fight, rest, equip and unequip plus current-format maps/monsters/items/character data and movement. Use complete independent action envelopes and deterministic cooldown/XP/inventory state. Reuse existing warm-up catalog support; no simulation/effects endpoint is necessary if unknown effects block. Default scenario rejection must remain atomic and reset/replay deterministic.
6. **Combat/equipment flow acceptance.** Run real services and real clients against TestServer, no host worker startup or credentials, with a fake cooldown waiter. Verify golden and gear sequences and every failure boundary below.

## Required failure acceptance

| Boundary | Expected observable behavior |
|---|---|
| Complete/invalid target | Zero actions; Completed or typed invalid-target reason |
| Unknown/unsafe stats, effects or access | Zero fight calls; explicit rejection facts |
| Rest unchanged/invalid/failed, defeat | Retain returned state, bounded terminal result; no revenge |
| Missing gear/condition/space, bad swap | No unsafe follow-up; exact inventory and slots from responses |
| Wrong movement or changed stats | Reevaluate or stop before fight |
| Full inventory, no XP, exhausted decision/fight/rest budget | Stop within documented limits; no remediation deletion |
| Cancellation | No new command; successful in-flight response retained |
| Lost response | One dispatch, terminal unknown outcome, no retry/recovery mutation |
| Reset/replay | Identical independent response bodies, decisions, state, command trace and virtual total |

## Subsequent part of the historical epic

The first equipment fixture uses pre-owned items. The historical Epic 6 also calls for loot/craft prerequisites. Add that as a separately buildable slice after HTTP combat acceptance: select one missing mob leaf, verify reciprocal drop-rate semantics, shared craft dependency accounting, actual ingredient conservation and slot changes. Only add crafting/workshop fixtures and routes required by that slice. Epic 5 does not waive this part of Epic 6 or falsely classify it as already implemented.

## External blockers and decisions

Live rollout remains no-go pending contract migration, access proof, ambiguous-outcome reconciliation and supported-mechanics calibration. Resolve turn-count semantics before widening the conservative cap. Resolve rest timing only if predicting time/cost; execution continues to honor responses. A permissive probability-of-death policy, autonomous defeat recovery, consumables, banking, elites/raids or paid API dependence is outside this ADR and needs an explicit next decision. No such decision blocks preparing or implementing the deterministic subset.
