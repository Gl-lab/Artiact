# R8a: Parent-bound healing consumables

## Scope

Support an explicitly configured consumable with exactly one immediate positive heal effect and no item conditions. Associate stock maintenance/use with an existing item goal. Produce a target stock through R7/R8, withdraw bank-owned stock before use, heal only below an explicit HP threshold, preserve reserve stock and stop supply after the parent completes. Maintain refill hysteresis from a minimum to a target; persist refill phase and charged use/material budgets with intents.

Use the existing GameClient.UseItem route, an atomic strategy command, exact inventory/HP/unrelated-state validation and read-only restart reconciliation. Compare optional recovery with the documented rest cooldown and configured production/use estimates; mandatory consumable preparation may disallow rest. Unknown or unsupported effects block.

## Acceptance

Independent scenarios start without food, gather/craft a batch, heal, and replenish after crossing the minimum. Bank stock avoids unnecessary crafting. Cooking/fishing preparation and constrained-capacity production are exercised separately and together where supported. Full HP consumes no food. Completed parent goals do not replenish. Lost replies retain charged budgets and never repeat use.

## Non-goals

Combat-triggered utility slots, timed buffs, arbitrary item conditions, defeat recovery, global optimization or live action approval.
