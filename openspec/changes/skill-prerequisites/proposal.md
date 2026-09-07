# R7: Item goals with skill prerequisites

## Why

R6 is published as `0d2a62c`. Item production currently stops at CraftSkillTooLow or NoSupportedResource. A final item should instead trigger bounded supported training without changing configuration between actions.

## Scope

Add an explicit preparation policy to item goals. Resolve missing crafting requirements before acquiring a high-level recipe's materials. Pick an available same-skill training recipe, acquire its ingredients through existing production, craft a single batch, verify skill progress, and replan. Retain training output in inventory/bank; do not destroy or trade it. For a required inaccessible resource, train its gathering skill on supported accessible resources first. Explain the parent item, prerequisite skill/target and selected training item in each proposal.

Training uses the parent's durable action/decision/time limits. Limit direct material units per training craft, which together with the run action cap establishes a finite total material bound. Recursion and dependency paths have explicit limits. Unknown effects, missing skill facts, zero-XP training, unsupported access, missing recipes and cycles fail closed.

## Non-goals

Capacity-aware batching/reservations (R8), consumable use (R8a), new combat mechanics, global XP optimization, automatic live actions, or assuming synthetic XP equals game XP.
