# Preregistered R14 comparisons

Before implementation, clone R9 autonomous-shield and autonomous-weapon as combat-discovery-shield/weapon. Preserve their action responses, gear, HP, combat XP and stock. Set gathering mining/fishing/woodcutting XP thresholds to1000 (slow but available alternative); existing weaponcrafting training stays unchanged. This distinguishes a useful combat route from cheap unrelated skill increments without changing production costs. The manual R9 stage portfolio and lowest-available gathering rule run from exactly the same new initial states.

Mandatory outcome: combat level3, required ward/water_blade equipped, two feathers and protected1 preserved, HP8/10. Shield ceiling12 commands/78 cooldown, weapon20/120, matching fixed R9. Four fights; no excess movement vs fixed. Lowest-skill is expected to miss the combat outcome within those action budgets. Replay each complete strategy result. Gear alternatives have no manual code in the autonomous policy. Test denied actions, expensive/unavailable preparation, ready gear, unknown effects, recovery food/rest and pending results separately. Repeat all R12/R13 comparisons unchanged; zero allowed regression.

These are predictions, not results. Any discrepancy must be retained and explained before rerunning the entire matrix; changing a formula or fixture to erase a loss is not acceptance.

## Results (2026-09-08)

Both original predictions pass: shield 12/78 and weapon 20/120, matching fixed commands/cooldown/movement and exact required gear/stock. Lowest-skill misses combat level3 in each bounded run. All three policies replay identically per scenario. R12/R13 accumulated comparisons also pass in the full solution gate.

An intermediate implementation lost the selected unlock's value after recovery/equip. Shield diverted to dummy and failed its scripted postcondition; weapon reached level3 in eight actions without required gear. Those failures were retained here and fixed by persisting the selected parent/opponent/value through its chain, not by changing utility, fixture or ceilings. A temporary eight-action diagnostic expectation was reverted; it exposed the incomplete chain once the parent was retained. Final acceptance uses the original predictions.
