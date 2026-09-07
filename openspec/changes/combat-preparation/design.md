# Design

Preparation is opt-in with positive combat target, monster and equipment. While weapon differs, the combat candidate is blocked and the equipment candidate delegates missing production to ItemProductionStrategy. A missing mob ingredient uses the configured opponent only, requiring its drop and current supported viable combat state. Rest/move/fight are atomic prerequisites. Recipes can contain multiple distinct loot leaves; replanning after every response counts actual stock rather than predicted drops.

The bounded prototype accepts only normal effect-free opponents and the existing fire-only weapon effect family. Unsupported loot/gear never bypasses CombatMilestoneStrategy. Acquisition success uses actual inventory; absent drops eventually exhaust the global budget.
