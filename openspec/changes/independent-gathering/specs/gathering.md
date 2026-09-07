# Behavior

- A nonempty valid skills list and omitted combat settings form a valid policy. Negative targets or orphan monster/equipment are invalid.
- A gathering candidate reads common identity/location/inventory and its level/xp/max_xp. Combat stats and effects do not determine gathering eligibility.
- Supported maps remain same-layer, standard access, no conditions/transitions. Resources remain one guaranteed positive bounded drop.
- Move preserves inventory and unrelated character state; gather changes only selected skill progress, permitted stock and cooldown. Changed identity/world is rejected.
- Profession-only observation reads maps/resources/character, probes their contract and move/gather, and does not request items/monsters or combat routes.
- Inspect has no POST actions. OneShot sends at most one command with existing freshness, cancellation, response validation and unknown-outcome guards.
