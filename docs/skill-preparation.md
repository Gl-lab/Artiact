# Item goals with skill preparation

R7 adds opt-in `Portfolio:Preparation` to item goals. Set `MaxIngredientUnits` (1–1000, default 100) and `MaxDepth` (1–16, default 12). An absent preparation object preserves previous production behavior and serialized policy identity. Enabling it changes the identity and cannot silently resume a different active policy.

The initial R7 subset is weaponcrafting training and mining resource prerequisites; [R8a](consumables.md) adds separately exercised cooking/fishing support. The wrapper scans the complete production plan for missing crafting requirements before acquiring final-recipe materials. It deterministically tries accessible same-skill recipes, produces one extra batch, and replans from actual stock and skill facts. Training output is retained; owned training products do not prevent another necessary batch. A candidate's `Prerequisite` explains its parent item, skill, target and training item while retaining the parent's selection identity and value. Parent completion stops preparation.

Training recipes require a positive item level, available skill requirement, empty conditions and a direct ingredient total no greater than `MaxIngredientUnits`. The recipe must remain in the nonzero-XP range. A training craft must increase the named skill's level or XP and preserve exact stock/other character fields. Unknown XP facts or a zero-progress reply stop the run. Missing/cyclic/inaccessible chains and depth exhaustion produce rejection. All prerequisites share the run's journal and action/decision/time budgets. Direct ingredients consumed by training crafts are bounded by `MaxIngredientUnits × MaxActions`; producing those ingredients also consumes the same action budget. This is a bound, not an optimal cost estimate.

When an item needs a higher-level mining resource, preparation first raises mining on eligible resources, then returns to the required source. Gathering excludes resources ten or more levels below the skill. The [official skill rules](https://docs.artifactsmmo.com/concepts/skills), read 2026-09-07, describe this zero-XP boundary and workshop/skill requirements. The planner verifies observed progress and does not assume synthetic XP equals game XP. Public OpenAPI was read without credentials to confirm mining/weaponcrafting level, XP and max-XP fields; preparation probes those fields.

Independent synthetic scenarios:

- `skill-preparation`: start weaponcrafting 1 with only protected=1; gather two ore across two trips, craft two training bars, reach weaponcrafting 2, craft a tool. Nine commands, 50 virtual cooldown seconds, final weaponcrafting 2/XP1, tool=1, bar=1, protected=1.
- `resource-preparation`: train mining with two ordinary ore, gather one rare ore at mining 2, then craft the tool. Seven commands, 40 virtual seconds, tool=1, ore=2, protected=1; weaponcrafting 1/XP1.

Both use real clients, exact command/stock oracles and terminal reconstruction. Lost training replies reconcile without another POST; action limits cover training and the final goal together. Mock skill rejection commits neither stock nor trace. No live training or new profession compatibility is claimed.

Optional [R8 capacity production](capacity-production.md) supplies batches and cross-goal reservations, including a constrained training scenario. Unsupported professions and inaccessible routes still block. Standalone crafting-skill milestones and equipment preparation do not yet use this item-goal wrapper. Their extension requires separate acceptance.

## R25: цели от потребностей

См. [Needs mode](needs-driven-goals.md): отдельные сохраняемые заказы, полный поддерживаемый путь, конечные срезы, исход NoActiveSupportedNeeds и ограничения. R20 этот режим не исполняет; live-приёмка новых цепочек не выполнена.
