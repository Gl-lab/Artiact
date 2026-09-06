# Combat experiment protocol — 2026-09-06

This protocol is fixed before prototype implementation. All named stats, items and map IDs below are synthetic and are not a recommended live route. Experiments live in `openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj`, outside the solution.

## Independent oracles

Use one active element, no special effects, normal opponents, bounded nonnegative stats. For prediction, ignore outgoing critical gains and charge every incoming hit as critical when its chance is nonzero. Assume the opponent acts first each exchange regardless of initiative. This deliberately overestimates incoming damage. Round each damage stage away from zero at midpoint. Reserve two actor turns per exchange; reject more than 50 exchanges pending resolution of upstream turn-count semantics. Safe means a worst-case remaining HP of at least 1; Unsafe means the conservative bound fails, not certain defeat. Unknown means insufficient or unsupported inputs.

| Name | Independent expectation |
|---|---|
| safe | Player HP 20, attack 10; monster HP 20, attack 3: two exchanges, loss bound 6, remaining 14 |
| exact-survival | Same opponent: HP 7 is Safe with 1 remaining, HP 6 is Unsafe with 0 |
| rounding | Attack 5 with 10% global + 0% elemental: 5.5 -> 6; 25% resistance: 4.5 -> 5; critical: 7.5 -> 8 |
| critical-bound | Monster attack 3 with nonzero crit becomes 5; player HP 10 survives two ordinary hits but fails the conservative 10-loss bound |
| tied-initiative | Same safe fixture with tied initiative: still charge two incoming attacks |
| no-damage | Outgoing attack zero or 100% resistance: Unsafe, no finite victory |
| turn-cap | Attack 1 versus HP 51: 102 reserved actor turns exceeds the research cap |
| unsupported | Missing stats, negative/out-of-domain values, multiple elements, effects or non-normal type: Unknown |
| equipment | Level-1 `quick_blade` attack 10 beats level-9 `heavy_blade` attack 4 against HP-20/attack-3 opponent: loss 6 versus 15. Baseline attack 2 loses 30 |

## Transition protocol

State is immutable and fully synthetic: character `researcher`, level 1, XP 0/10, HP/max 20/20, attack 10, free units 10, map 1, weapon `quick_blade`; monster is on map 2. Target is level 2. Default limits are 20 decisions, 4 fights, 2 rests and 3 consecutive commands without combat XP progress. Decision budget includes terminal reporting; at most 19 commands under this default.

The golden script is Move (map 2, cooldown 2), Fight (HP 14, XP 5, free units 9, cooldown 7), Rest (HP 20, cooldown 13), Fight (level 2, XP 0, HP 14, free units 8, cooldown 7), then Completed. Expected: five decisions, four commands, two fights, one rest, 29 virtual seconds. Rest-before-each-fight is a conservative experiment policy; cooldown 13 is an injected response, not a formula prediction.

Every deterministic test executes twice from fresh objects and compares full state, decisions, command order, counters and virtual time as well as independent literal assertions. Additional scripts cover all failure groups in design.md: terminal target/invalid inputs, budgets, no XP progress, low/invalid/unchanged rest HP, defeat/relocation, gear conditions/slot/space/failure, changed destination/stats, stale observation, cancellation and response loss. Failed/unknown attempts remain charged; terminal calls are sticky and cannot dispatch again. Rest/gear/movement do not reset the no-XP counter.

## Payload probes

Authored fragments exercise actual existing DTO deserialization without network: `data.characters` and `data.fight` do not populate ActionData.Character; equipment is an array with string slots, unlike Inventory; map content nests under interactions; optional missing monster effects are not proven to mean no effects. Fragments deliberately omit unrelated required fields and are not full schema-valid acceptance fixtures. A separate strict prototype participant reader checks exact-name uniqueness and required nonnegative HP, including duplicate/missing participants. Full envelopes belong to Epic 6.
