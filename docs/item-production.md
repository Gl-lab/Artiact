# Independent item production

R8 adds optional [capacity-aware production](capacity-production.md): one-batch planning, protected stock, other-goal quotas and inventory-pressure deposits. Bank-owned final output contributes to completion without unnecessary withdrawal.

R7 adds optional [skill preparation](skill-preparation.md) to item goals: supported weaponcrafting training and mining prerequisites share the parent's budgets. Without `Portfolio:Preparation`, low skills retain the previous refusal behavior.

R4c adds explicit `Portfolio:PrepareEquipment=true` with combat target/opponent/weapon. The equipment candidate obtains missing materials through the same production ledger, including multiple drops of the configured supported opponent. The combat milestone waits for the weapon. Acquisition fights use current equipment viability, and all steps share the journal/budgets. A bank-owned weapon is withdrawn before equip. Broader opponents/gear, consumables and defeat recovery remain excluded.

R4a adds `Portfolio:Items` entries with `Code`, positive `Quantity` (up to 10000) and `Value` (default 30). Skills may be empty for an item-only profile. Completion counts target inventory plus observed bank stock. Item goals load item catalogs and probe crafting; enabled bank support additionally probes withdrawal.

The pure plan reserves one shared ledger across sorted nested recipe ingredients and preserves batch surplus. It rejects cycles, invalid quantities/conditions, unavailable leaves and bounded depth/step/arithmetic failures before dispatch. Runtime reevaluation checks selected recipe skill, supported workshop access and inventory capacity. Missing resource leaves use the gathering strategy restricted to that output. General mob-drop acquisition is deferred to R4c.

Only one command executes per tick. Required bank stock is withdrawn in exact bounded quantities through the official array transaction contract; this does not authorize arbitrary bulk withdrawals. Craft responses validate output items/cooldown and exact stock, preserving unrelated character fields. Both commands use the R2 journal and read-only restart reconciliation. The policy's deposit allowlist should retain ingredients needed by production to avoid unproductive capacity shuttling; shared budgets stop such loops.

Synthetic `item-production`: gather two ore, make one bar, then one tool. Six actions and 32 virtual seconds leave only tool=1 and protected=1. `item-production-bank` begins with two bank ore and uses five actions/25 seconds; bank ends empty with the same inventory. Neither fixture claims live craft XP/cooldown mechanics. Tests also cover shared/batched ingredients and lost craft/withdrawal replies across restart. [Evidence](../openspec/changes/item-production/execution-evidence.md).

## R25: цели от потребностей

См. [Needs mode](needs-driven-goals.md): отдельные сохраняемые заказы, полный поддерживаемый путь, конечные срезы, исход NoActiveSupportedNeeds и ограничения. R20 этот режим не исполняет; live-приёмка новых цепочек не выполнена.
