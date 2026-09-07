# Capacity-aware item production

Enable `Portfolio:CapacityAwareProduction=true` with a bank policy. `Portfolio:ProductionReserves` maps item codes to nonnegative quantity floors across inventory and bank. The absent production policy preserves the previous serialized policy identity. Bank deposit permission still comes exclusively from `Portfolio:BankRetain`; the planner does not authorize arbitrary deposits or destruction.

Production counts final output across inventory and bank, plans one additional root recipe batch, and splits intermediate crafts into individual recipe executions. It does not withdraw completed output merely to place the entire order in inventory. When a partially bank-owned intermediate needs further production, withdrawal is delayed until its missing portion has been made, preserving working space.

The planning stock view protects configured reserves and already-owned quotas of other item goals. Reservations prefer stock already in the bank, avoiding unnecessary retrieval of that protected quota. They do not alter observations: dispatch/preflight/postconditions still compare the exact real inventory and bank, and every command is selected from fresh state. This is local sequential allocation, not a distributed inventory reservation service.

Inventory pressure derives a temporary retain policy for one batch of the nearest upcoming craft plus protected inventory. Allowed surplus can move/deposit through existing bank commands. If the minimal recipe plus protected inventory cannot fit, `MinimalRecipeExceedsCapacity` blocks before movement. A full/inaccessible bank or no allowed excess also blocks. All transitions share the session journal and budgets; deposits are not productive progress.

Independent local evidence:

- `capacity-production`, tool target 5, inventory capacity 3 including protected=1: 42 commands / 228 virtual cooldown seconds. Ten gathered ore become five tools; final bank tool=4, inventory tool=1/protected=1. No ore or bars remain.
- Competing bar=1 and tool=1 goals with the same capacity retain both quotas: 14 commands / 77 seconds. The bank keeps the reserved bar while the freshly produced bar is consumed for the tool.
- `capacity-training` combines R7 weaponcrafting preparation and tool target 3 with capacity 3; preparation and final output complete without configuration changes, preserving protected stock.
- Lost deposit, withdrawal and craft replies reconcile after reconstruction without a second POST. Minimal-capacity and full-bank cases stop before unproductive shuttling.

These are bounded planning heuristics, not shortest-route or maximal-batch optimization. Some feasible chains involving intricate temporary storage or a protected floor larger than the working inventory can still block. Training supports the R7 profession subset. Explicit equipment preparation uses the existing item producer; autonomous skill training for equipment remains a later integration. No new live banking/crafting acceptance is claimed.
