# Consumable production

## ADDED Requirements

### Requirement: Conditional healing
The system SHALL use only configured supported heal consumables under the HP threshold, never at full HP, and SHALL cap quantity by useful healing, stock reserve and run allowance. Unknown effects, conditions or invalid response state SHALL block.

### Requirement: Parent-bound supply
Supply SHALL retain the parent candidate identity, use R7/R8 for materials/skills/capacity, and prefer existing bank stock. Minimum-to-target refill phase SHALL survive restart. Completed parent goals SHALL end replenishment; standalone quantity goals SHALL complete by quantity.

### Requirement: Durable budgets and outcome
Use/material charges and refill phase SHALL be persisted before POST. Verified inventory and HP effects SHALL be saved before cooldown. Unknown outcomes SHALL permit only reading/reconciliation, with no refunded budget or repeated use. Refill work SHALL NOT indefinitely reset the parent's no-progress guard.

### Requirement: Recovery choice
Optional consumables MAY yield to explicitly permitted rest using explained time/stock assumptions. Mandatory infeasible preparation SHALL return a bounded rejection. Combat utility/effect paths remain excluded.
