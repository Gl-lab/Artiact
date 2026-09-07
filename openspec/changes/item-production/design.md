# Design

Pure recursive planner walks sorted recipe ingredients with one shared stock ledger. It consumes available stock, recursively schedules missing leaves/crafts and retains batch surplus. Bound depth, operations and arithmetic. Planning completes before choosing its first action; unavailable later ingredients do not trigger partial work. Replan after each atomic response.

Configured item goals add item catalogs and crafting contract probe. A supported missing gathering leaf invokes the common gather strategy filtered to that exact drop. Bank-owned required stock generates a bounded withdrawal after supported bank movement; withdraw uses the bank transaction envelope and exact conservation. Craft validates skill, ingredients, workshop, post-stock and output details, and preserves unrelated state.
