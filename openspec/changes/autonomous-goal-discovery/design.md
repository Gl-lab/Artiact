# Design

## Policy and bounds
`Portfolio:AutonomousGoals=true` opts into discovery-v1. Manual Skills, Items, CombatTarget, Monster, Equipment, preparation and consumable parents cannot be mixed with it. Execution remains Inspect-only in R11. Default manual mode is unchanged. No new shared DTO is needed.

Supported discovery categories are mining, woodcutting, fishing and alchemy. The verified upstream skill cap is 50 (official skills concepts, inspected 2026-09-08); it is a versioned algorithm constant, not a user target. At most 32 resource alternatives per skill and 4 generated milestones are considered. Catalogs larger than 4096 resources or 16384 items fail closed. Duplicate codes fail closed. Missing skill fields reject that category without requiring combat fields.

## Utility v1
Find the nearest higher resource level reachable on a standard map in the current layer. The route must have a currently executable training resource, whose XP does not become zero before that milestone. Unlock utility is 1 when the new resource provides a strictly higher gathering XP base at the target level or preserves training beyond the old resource's ten-level XP window. Ordinary next-level progress is worth 0.1. Equal scores use ordinal candidate ID. No XP from different professions is added.

Recipe utility is zero unless an active supported need is proven; R11 has no healing or combat parent and explicitly reports this uncertainty. The items catalog is observed so the explanation can list dependent recipes without assigning arbitrary value to every ingredient. Existing bank policy remains explicit and stock is observed only when permitted. R13 will introduce needs, rather than silently reward stock accumulation here.

FullPathStrategy supplies route cost, including disclosed future-XP and bank-trip assumptions; discovery supplies value and provenance. Reject paths whose estimated actions exceed the available action bound, whose estimated cooldown exceeds the time bound, or whose training resource cannot span the milestone. Estimates are not completion guarantees. Inspect exposes target, resource unlock, utility components, algorithm version, assumptions, and reevaluation trigger.

## Integration
Use a discovery strategy that wraps generated ResourceAlternatives in FullPathStrategy against a transient derived policy. Stable user policy identity remains on observations. Do not mutate user settings. Candidate discovery evidence is an optional application-owned record. An empty discovery result produces a typed rejected candidate so existing session validation remains fail-closed; it must not claim TargetsReached.

## Verification
First exercise configuration through existing binding to demonstrate the missing behavior before implementation. Then cover deterministic discovery, full-path selection, budget rejection, unsupported observations, manual identity and socket-free factory Inspect. Execute the solution gate and self-review the final diff before publication.
