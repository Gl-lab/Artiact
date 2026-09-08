# Design and preregistration (before implementation)

Compare auto, fixed portfolio and lowest-available-next-skill with identical starting state and action/time limits per scenario. Record actual commands, confirmed cooldown, all moves (conservative upper bound on unproductive movement), dispatched candidate switches, per-skill levels/XP and stock. Do not sum XP across professions. Execute each complete result twice and compare semantic state/counters; wall-clock timing is not a deterministic gameplay oracle.

## Additional outcomes and ceilings

1. Long gathering: existing discovery-new, action limit80, decisions150, no-progress80, duration2000. Mandatory woodcutting11/mining11 with XP0 each, wood20/ore20, no bank/other actions. Auto expected41 commands/207 cooldown/1 move and20 completed parent levels. Fixed portfolio woodcutting11/mining11, equal values1 and full paths, expected41/207/1. Lowest expected60/340/20. Stop measuring on the common outcome; then verify auto stops Blocked when no supported XP route remains and never exceeds budgets after reopening.
2. Near-full inventory: new discovery-full alias, original discovery-new except capacity2 and one initial wood. Explicit BankRetain wood0/ore0, no reserve consumption. Mandatory woodcutting2, total wood3 for auto/fixed. Auto/fixed expected5 commands/27 cooldown/2 moves (gather, bank delivery, gather). Lowest expected12/68/6 to achieve woodcutting2 after mining2; its additional ore2 is reported separately. Action budget16/decisions80/no-progress40/duration1000. Bank transfers preserve aggregate stock. Replay all three.
3. No route: new discovery-no-path alias with resource destinations conditional, current empty standard map1. All three policies must return Blocked with0 commands/cooldown/moves; fixed woodcutting2 and lowest next mining cannot manufacture an accessible route. This is refusal, not NoUsefulSupportedGoals.

## Accumulated operational coverage

Retain new/uneven/bank/locked R12, training/production/bank/capacity R13 and shield/weapon R14. Expensive preparation is represented by whole-path material and permission rejection, not a relaxed cost ceiling. Changed catalog tests reject/suppress the parent; unknown result plus catalog change remains unresolved without another POST. Existing six child-process kill points cover manual/autonomous before acceptance, after acceptance and verified-before-wait. Extend lost-use coverage to the combat parent and verify a long-run reopening against the same counters.

Initial predictions may be wrong: retain discrepancies, explain any protocol amendment and repeat the entire accumulated matrix. No score/fixture tuning is planned. The live protocol must specify Inspect-first, named origin/character/action bounds, freshness/contract gates, durable RunId and no replay, with separate explicit authorization before action. No scheduler/24x7/hosting is added.

## Amendment 1 — prediction corrections, 2026-09-08

The long-run initial prediction understated both auto and fixed cost: each uses42 commands/214 seconds/2 moves and2 candidate switches. Full-path estimates change with level and cause an intermediate switch and return; the theoretical one-move itinerary was not the behavior of either baseline. Keep this suboptimality visible. The zero-regression allowance remains zero against actual fixed42/214/2; lowest60/340/20 is unchanged. No formula, fixture or useful outcome changed.

The near-full lowest rule deposits two item codes per bank action. Scripted cooldown is3 seconds per code, so actual12 commands/74 seconds/6 moves corrects68; auto/fixed5/27/2 is unchanged. All three policies and the complete accumulated matrix must be replayed after correcting assertions. These are corrected baseline predictions, not removal of failed outcomes or a positive regression allowance.
