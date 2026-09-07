# Design

An optional autonomous policy contains ordered level stages, each with a bounded opponent allowlist, and a bounded equipment allowlist. It replaces the manually configured combat/equipment strategies. The first unfinished stage is the parent of every preparation command. Evaluate current gear and each single-slot replacement; only supported, safe projections with an executable next prerequisite qualify. Rank by estimated preparation, travel, fight and recovery cost, with deterministic ties. Expose opponent, slot, item, loss bound and cost assumptions on candidates. Recompute after every verified observation; do not invent simultaneous inventory or a two-slot combination.

Projection supports weapon/shield static elemental attack, damage, resistance and critical-strike modifiers; requires known current gear and exact subtraction/addition. HP modifiers, conditions, active effects, utilities and runes are excluded. Equipment postconditions preserve unrelated fields and check exact stock and all changed stats. Existing standalone combat remains compatible.

Immediate food may bind to parent `combat`; supply budgets and refill markers use R8a. Food and rest happen outside combat. Unknown forecast, no feasible route, defeat and unverified replies retain existing bounded-stop semantics. Candidate identity survives a pending command across restart.

Costs initially use configured per-action estimates and the finite production plan, with an explicit training uncertainty allowance; R10 adds measurements and full-path reporting. Current production feasibility is checked by the actual R7/R8 strategy, not by cost alone.
