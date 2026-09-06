# Preliminary source observations — 2026-09-06

These observations scope the research. They are not a completed mechanics matrix, runtime verification or model decision. Recheck sources during execution.

Research was subsequently completed: see [final matrix](../../../docs/research/combat-equipment/contract-matrix.md), [ADR](../../../docs/decisions/0001-combat-viability-and-recovery.md) and [execution evidence](execution-evidence.md). The observations below remain the historical planning snapshot.

## Local anchors

Base: `4b49b1e` (bounded mining implementation). Relevant files: `Artiact/Client/GameClient.cs`, `Artiact.Contracts/Models/Api/ActionData.cs`, `Artiact.Contracts/Models/Api/Inventory.cs`, `Artiact.Contracts/Models/Goal.cs`, `Artiact/Resolvers/TargetLootingResolver.cs`, `Artiact/Services/GoalDecomposer.cs`, `Artiact/Services/StepBuilder.cs`, `Artiact/Services/CraftTargetEvaluator.cs` and `docs/known-limitations.md`.

## Public API inspection

The [official OpenAPI](https://api.artifactsmmo.com/openapi.json), retrieved on 2026-09-06, reports version **8.2.3**. Initial gaps:

| Operation/schema | Observed contract | Local implication |
|---|---|---|
| POST /my/{name}/action/fight | CharacterFightDataSchema requires cooldown, fight and characters | ActionData expects a singular character and has no fight model; research must define participant reconciliation |
| POST /my/{name}/action/equip | Array of EquipSchema; slot references ItemSlot | EquipItem serializes one Inventory with an integer slot |
| POST /my/{name}/action/unequip | Array of UnequipSchema | UnequipItem has the same object/slot mismatch |
| POST /my/{name}/action/rest | CharacterRestDataSchema includes hp_restored and character | Generic ActionData lacks hp_restored; compare retained versus discarded information |
| POST /simulation/fight | Simulation schemas; member/founder restriction | Evaluate as optional evidence, not a required production dependency |

These are shape observations, not tested failure claims. No authenticated request or action was made.

## Mechanics sources

- [Combat and stats](https://docs.artifactsmmo.com/concepts/stats_and_fights/): damage/resistance rounding, critical randomness, initiative ties, finite turns and defeat state need explicit fixture coverage. Documentation currently describes a 100-turn cap and defeat relocation to (0,0) with 1 HP; prototype expectations must be dated and rechecked.
- [Equipment](https://docs.artifactsmmo.com/concepts/equipment/): source for slot, item-condition and equipment-change research.
- [Resting and using items](https://docs.artifactsmmo.com/concepts/resting_and_using_items/): source for recovery research.
- [Actions and cooldowns](https://docs.artifactsmmo.com/concepts/actions): source for action lifecycle and returned cooldown semantics.
- [Fight simulator](https://docs.artifactsmmo.com/members/fight-simulator/): source for optional simulation capabilities and access restrictions.
- [Maps and movement](https://docs.artifactsmmo.com/concepts/): navigate to current movement guidance for layers/access; current coordinate-only local maps are insufficient reachability evidence.

Formulas, effect coverage, exact defeat/drop consequences, numeric risk limits and the final model remain research tasks. Existing loot level heuristics and synthetic mining outcomes do not answer them.
