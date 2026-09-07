# Capacity production

## ADDED Requirements

### Requirement: Order versus batch
The system SHALL complete an inventory-plus-bank quantity goal even when the full order cannot fit in inventory, provided its minimal recipe can fit. Bank-owned final products SHALL NOT be withdrawn merely to assemble the full order in inventory.

### Requirement: Protected stock
Reserved quantities and owned final quantities serving other goals SHALL NOT be consumed by a competing production plan. Commands SHALL validate exact actual inventory/bank changes against fresh observations.

### Requirement: Productive bank transitions
The system SHALL deposit only explicitly allowed excess and retain the next craft's ingredients. Impossible minimal recipes, protected conflicts and inaccessible/full banks SHALL produce a bounded explanation rather than endless deposit/withdraw loops.

### Requirement: Recovery
Batching SHALL reuse the same durable intent, budgets and read-only reconciliation. Lost crafting or bank responses SHALL NOT authorize a repeat POST. Preparation and final production SHALL share the run budget and stop after all parent goals complete.
