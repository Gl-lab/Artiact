# Parent-bound healing consumables

R8a adds `Portfolio:Consumable` for one configured parent item goal. Configure `ParentItem`, `Code`, `HpBelowPercent` (1–100), `MinimumStock`, `TargetStock`, `Reserve`, `MaxUsed`, `MaxMaterialUnits`, `AllowRest`, `UseSeconds` and `PreparationSeconds`. Defaults are threshold 50%, minimum 1, target 2, reserve 0, maximum used 10, material units 100, mandatory use path (`AllowRest=false`), estimated use 3 seconds and preparation 30 seconds. The parent must exist in `Portfolio:Items` and differ from the food code. No consumable policy means no added use/supply behavior and preserves the prior policy identity.

Only a consumable with exactly one positive `heal` effect and no conditions is supported. The current public cooked-gudgeon record has heal=75 and a cooking-1 recipe consuming one gudgeon; this was read without credentials on 2026-09-07, not used live. The [official use rules](https://docs.artifactsmmo.com/concepts/resting_and_using_items) cap healing at maximum HP and describe a fixed use cooldown and percentage-based rest. Execution honors validated returned cooldown; policy times are decision estimates.

The wrapper checks parent completion first. When healing is needed, it produces a target batch through R7/R8 or withdraws existing bank food. Quantity is capped by useful HP, available stock, reserve and remaining charged-use allowance. Full HP never consumes food. After use depletes stock below the minimum, refill continues to the target, with its phase preserved across restart. A completed parent ends refill. An initially healthy parent without a previous use does not acquire food merely to satisfy the minimum. Independent item-quantity goals retain their own completion rules.

Protected production floors and other final food-goal quotas also constrain consumption. A target stock incompatible with those floors gives `ConsumableReserveConflict`. Optional rest can replace healing when its missing-HP estimate is no worse than use plus configured preparation, or when the use allowance is exhausted. Mandatory infeasible preparation blocks. Unknown effects/conditions, changed stock, mismatched returned item, HP or unrelated fields all fail closed.

Use and supply material charges are recorded with intent before POST. They are conservative reservations: rejected and unknown attempts do not refund the allowance. Material units count direct ingredients of crafts performed for supply, including training crafts and intermediate transformations. The offline run result exposes `ChargedResources`; do not interpret unknown charges as verified consumption. The refill phase, baseline run context and selected candidate identity persist for reconstruction. The selected candidate disambiguates commands shared by competing goals. Older pending checkpoints lacking it still require a unique reconstructable command.

Supply commands do not reset the parent's no-progress count. `Execution:MaxNoProgress` is now configurable (default 10, positive and no greater than MaxDecisions). Longer supply chains require an explicit sufficient bound; local scenarios use 60. Increasing it does not remove the shared action/time limits or use/material ceilings. Verified use effects are persisted before cooldown; response loss allows only observation and exact reconciliation, not a repeated use.

The supported R7 training subset now additionally includes cooking and fishing, with corresponding schema probes and a combined training scenario. This does not enable arbitrary professions, item conditions or combat effects.

Independent mock scenarios use synthetic `meal` (heal=8) and `snack` (heal=4), not live item recipes or live XP:

- `consumable-production`: starting HP4/20, manufacture two meals, use both, replenish two, then finish tool=1. Reconstructing after every tick yields 23 actions/127 cooldown seconds, HP20, protected=1, remaining meal=2, charged use=2 and materials=4.
- `consumable-capacity`: the same with capacity 3 deposits finished food to make working space; 25 actions/137 seconds, same total stock and effect.
- `consumable-bank`: four existing bank meals avoid food crafting; 11 actions/51 seconds, remaining meals=2, zero food material charge.
- `consumable-training`: missing cooking and fishing requirements are acquired before meal production; the parent completes with HP restored.
- `consumable-full`: full HP follows the six-command parent production without use or food crafting.

Tests additionally cover lost use responses, immutable terminal completion, material/no-progress ceilings, optional rest after use cap, reserve conflicts and unsupported/mismatched effects. Live use, utility equipment slots, automatic combat consumption, timed buffs and defeat recovery remain outside this delivery.
