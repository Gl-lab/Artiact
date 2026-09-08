# Autonomous recovery (R13)

Add `Portfolio:Recovery` to an autonomous profile to discover a useful HP recovery goal. No item code, value, combat parent or manual goal is required. Recovery is absent by default and adds no HP dependency to ordinary gathering profiles.

Recovery settings: HpBelowPercent defaults to 50; AllowUse, AllowCraft, AllowRest and AllowBankWithdrawal default to false. MaxUsed defaults to 10, MaxMaterialUnits to 100. Reserved supplies explicit stock floors. Bank operations for recovery additionally require BankRetain and AllowBankWithdrawal; without them, production can use inventory and gathering but cannot transfer bank stock. User permissions never expand because a goal looks useful.

Below the threshold, recovery-v1 assigns one parent utility of `4 × deficit / max_hp`, then compares allowed rest, ready stock and finite food production using full-path cost. Supported food has exactly one positive heal effect and no conditions; at most 32 ordinal consumables are considered. Quantity covers the observed deficit plus retained food. Recipe materials and training receive no separate utility. One parent is selected, and stock is replanned after every authoritative observation; unselected food alternatives reserve nothing.

The selected HP parent remains until full recovery or rejection/cancellation. Food/rest alternatives can change within that parent when stock or cost changes. Cooking/fishing prerequisites use the existing bounded preparation planner; future training and cooldown remain estimates. Reserve-aware production works without bank access and blocks if working capacity is insufficient. Permitted bank access enables existing capacity batches. Once HP is full, the parent ends and refill stops. Supply does not reset parent no-progress; confirmed healing does. Long training chains need an explicit sufficient MaxNoProgress (the training acceptance uses 60).

Use and supply intent charge global `recovery:use` and `recovery:materials` across item alternatives. Material units include training and intermediate craft inputs. Unknown/rejected attempts do not refund charges. Version-2 checkpoints retain recovery-v1 provenance, original pending context and completion HP. Later damage does not erase historical recovery. `run-result` reports HP progress and charged resources; only confirmed HP changes count as useful recovery progress.

The [preregistered protocol and comparisons](../openspec/changes/autonomous-recovery/comparisons.md) use synthetic meal heal8, not live item data:

- `recovery-training`: HP4→20, fishing/cooking preparation, two meals used; 20 actions/112 confirmed cooldown seconds. Final cooking3/fishing4, protected1/baitfish2/snack2/meal0; uses2/materials4.
- `consumable-production` and capacity3: HP4→20 in 9 actions/49 seconds, uses2/materials2, no leftover food/refill.
- `consumable-bank`: withdraw two of four meals and use them; 3 actions/13 seconds, two bank meals retained, no material charge.

Tests cover full HP, thresholds, stock floors/shared ingredients, forbidden use, unknown effects, cycles, material bounds, no-bank operation, optional rest, cancelled parents and lost/invalid use replies. Existing gathering quality/recovery tests remain mandatory. [Official rest/use rules](https://docs.artifactsmmo.com/concepts/resting_and_using_items/) were inspected on 2026-09-08; local synthetic tests do not prove live compatibility. R14 reuses recovery for [combat prerequisites](combat-goal-discovery.md).

## R25: цели от потребностей

См. [Needs mode](needs-driven-goals.md): отдельные сохраняемые заказы, полный поддерживаемый путь, конечные срезы, исход NoActiveSupportedNeeds и ограничения. R20 этот режим не исполняет; live-приёмка новых цепочек не выполнена.
