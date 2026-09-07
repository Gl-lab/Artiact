# Autonomous combat preparation (R9)

The existing portfolio configuration accepts `AutonomousCombat`:

```json
{
  "CombatTarget": 3,
  "Monster": "dummy",
  "Equipment": "",
  "AutonomousCombat": {
    "Stages": [{ "Target": 2, "Monsters": ["dummy"] }, { "Target": 3, "Monsters": ["guardian"] }],
    "Equipment": ["ward", "water_blade"]
  },
  "Preparation": { "MaxIngredientUnits": 100, "MaxDepth": 12 }
}
```

These names belong to local synthetic scenarios. Normal action consent, origin, checkpoint and run budgets still apply. Limits: ten stages, ten opponents per stage, twenty gear alternatives. Increasing stage targets end at `CombatTarget`. Do not combine this mode with manual Equipment/PrepareEquipment/MonsterAlternatives. `Monster` remains the existing item-loot preparation fallback.

The service compares ordinary effect-free opponents and single weapon/shield replacements. Static elemental attack, damage, resistance and critical properties are supported. Other equipped slots, conditions, HP modifiers, runes and utilities are rejected here. Exact subtraction must remain valid. Equipment acquisition uses R7/R8, followed by atomic unequip/equip and exact inventory/stat verification. No combined two-slot search or defeat recovery is implemented.

`CombatRoute` exposes target, opponent, slot/item, maximum loss and preparation estimate. Finite production steps, travel, equipment, fight and recovery contribute to configured cost; training adds a disclosed uncertainty allowance. An executable next prerequisite is required independently of cost. This estimate is not a proof of an optimal path; full-path measurement is R10.

`Consumable.ParentItem = "combat"` attaches [immediate healing](consumables.md). Budgets/refill phase survive restart and stop with the parent. Use happens outside battle. The core fight strategy requires full HP: food applies below its configured threshold, otherwise ordinary rest applies. `AllowRest` compares recovery against use and its configured supply estimate.

Local oracles: `autonomous-shield` reaches level 3 in 12 actions/78 cooldown seconds, with ward, HP 8, feather 2 and protected 1. `autonomous-weapon` trains weaponcrafting 2 with two retained bars and equips water_blade: 20 actions/120 seconds, HP 10, feather 2, bars 2, quick_blade 1 and protected 1. Both fight twice per stage. Stage-two maximum losses are 12/10 respectively. The food variant produces six meals, charges four uses and retains two.

Defeat and invalid verified results stop. Unknown dispatch outcomes require fresh-state reconciliation; unresolved ambiguity never retries a POST. Pending equipment can reconcile without replay. These are local results only. [ADR 0001](decisions/0001-combat-viability-and-recovery.md) retains the live no-go; see the [mechanics matrix](../openspec/changes/autonomous-combat/mechanics.md).
