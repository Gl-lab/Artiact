# Autonomous quality evidence — 2026-09-08

Scope: deterministic R11–R15 offline acceptance. Each row runs auto, fixed and lowest-available-next-skill from the same initial state and limits, then replays all three. Useful outcomes are defined before running; no cross-skill XP sum is used. The [R15 protocol](../openspec/changes/autonomous-quality/design.md) preserves initial prediction errors and amendments. [Live acceptance](autonomous-live-protocol.md) is separate and unexecuted.

Policy versions: discovery-v1, recovery-v1, combat-discovery-v1; default unknown multiplier2/switch ratio1.1 where measured selection is enabled. Fixed portfolios and limits are defined in the original [R12](../openspec/changes/autonomous-goal-execution/comparisons.md), [R13](../openspec/changes/autonomous-recovery/comparisons.md), [R14](../openspec/changes/combat-goal-discovery/comparisons.md) and R15 protocols. No policy formula was tuned in R15.

Cells show **commands / confirmed cooldown seconds / moves / dispatched candidate switches**. All moves are reported as a conservative upper bound on unproductive movement; useful transfer/training moves are not silently removed. A missed outcome cannot win merely by stopping early or having less cooldown.

| Scenario / required useful outcome | Auto | Fixed | Lowest rule | Assessment |
|---|---|---|---|---|
| New: woodcutting2 | 2 /10 /0 /0 | 2 /10 /0 /0 | 6 /34 /2 /1 | Fixed tie; lowest costs more |
| Uneven: mining10 unlock | 2 /10 /0 /0 | 2 /10 /0 /0 | 20 /104 /2 /1 | Fixed tie; lowest costs more |
| Bank: woodcutting2, retained ore10 | 2 /10 /0 /0 | 2 /10 /0 /0 | 6 /34 /2 /1 | Fixed tie; lowest costs more |
| Locked iron: reachable woodcutting2 | 2 /10 /0 /0 | 2 /10 /0 /0 | 2 /10 /0 /0 | Tie |
| HP training: HP4→20 with fishing/cooking preparation | 20 /112 /9 /0 | 20 /112 /9 /0 | 73 /429 /27 /20; HP4 | Fixed tie; lowest misses HP |
| HP production: HP4→20 | 9 /49 /4 /0 | 9 /49 /4 /0 | 70 /406 /26 /19; HP4 | Fixed tie; lowest misses HP |
| Ready bank food: HP4→20 | 3 /13 /1 /0 | 5 /19 /1 /0 | 70 /406 /26 /19; HP4 | Auto wins; lowest misses HP |
| Shield: level3, ward, HP8 | 12 /78 /3 /2 | 12 /78 /3 /2 | 12 /70 /5 /4; level1 | Fixed tie; lowest misses combat |
| Weapon: level3, water_blade, HP10 | 20 /120 /6 /2 | 20 /120 /6 /2 | 15 /87 /6 /5; level1 | Fixed tie; lowest misses combat |
| Long: mining11/woodcutting11 | 42 /214 /2 /2 | 42 /214 /2 /2 | 60 /340 /20 /19 | Fixed tie; lowest costs more |
| Capacity2 with initial wood1: woodcutting2 | 5 /27 /2 /0 | 5 /27 /2 /0 | 12 /74 /6 /1 | Fixed tie; lowest costs more |
| No accessible resource: explained refusal | 0 /0 /0 /0 | 0 /0 /0 /0 | 0 /0 /0 /0 | All Blocked; not successful exhaustion |

The equivalent-outcome default regression allowance is zero; no row exceeds fixed commands/cooldown/moves. The measured-win requirement holds independently in early gathering, bank food, long gathering and capacity cases. No accepted row loses to a baseline on an equivalent mandatory outcome. This does not claim optimality: the long auto and fixed routes both make an extra return compared with a theoretical one-move itinerary. Against fixed there is one command/cooldown win and ten useful-outcome ties, plus one common refusal. Against lowest there are ten wins by cost or required outcome, one useful tie and one common refusal.

## State and stock evidence

- Early gathering finishes its specified level with XP0; uneven opens mining10. The lowest rule also gains unrelated levels in several rows; those do not replace the specified unlock. Bank ore10 is retained.
- Long run completes twenty autonomous level parents, ends mining11/woodcutting11 with XP0 each and ore20/wood20. There are no bank/actions outside gathering and movement. A further observation returns NoFeasibleCandidate, which remains terminal after reconstruction without budget refund or another POST.
- Capacity2 auto/fixed end with total wood3 across bank/inventory (initial1 + two gathers). Lowest additionally has ore2. Transfers preserve aggregate stock. The bounded capacity3 HP case separately reaches HP20 in9/49 with no refill after completion.
- HP training ends cooking3/fishing4 with protected1/baitfish2/snack2/meal0; uses2/materials4. Plain production uses2/materials2 and leaves no healing stock. Ready bank uses2, retains two of four bank meals and charges no craft materials. No Fight POST appears in HP-only runs.
- Both combat rows retain protected1/feather2; weapon training retains bar2 and the unequipped quick_blade. Gear/training material charges are2/4. Only confirmed fight progress is useful combat XP; healing supply does not reset its parent counter.

## Operational matrix

Final skill-local counters (level/XP) make side effects explicit. Auto/fixed HP training ends fishing4/0 and cooking3/0; plain food production ends fishing2/0 and cooking2/0; bank food changes no skill. Lowest ends mining11/0 and fishing12/0 in HP training, mining11/0 and fishing11/0 in the other HP cases, while cooking remains1/0 and HP4.

Auto/fixed shield ends combat3/0 and weaponcrafting1/1; weapon ends combat3/0, mining2/0 and weaponcrafting2/1. Lowest shield ends combat1/0, mining2/5 and fishing3/0; lowest weapon ends combat1/0, mining3/0 and fishing3/5. These additional gathering benefits are reported despite missing the combat outcome. Counters are not summed lifetime XP.

| Boundary | Verified behavior |
|---|---|
| Expensive/forbidden preparation | Whole recovery/gear material paths reject at allowance1; disallowed Use/Fight/Craft/Equip and unsupported effects/cycles produce no corresponding command |
| Changed catalog | Active gathering parent is rejected and suppressed; a supported alternative may proceed within the same counters |
| Catalog change during uncertain POST | Remains UnknownOutcome; no new parent or POST |
| Lost gather, gear, Fight, recovery Use and combat-parent Use | Reconstruct original pending baseline; reconcile fresh postcondition without replay; no invented cooldown/facts for missing replies |
| Failed durable selection/completion write | No unjournaled action; retained intent reconciles after accepted action |
| Process kill at three boundaries | Six manual/autonomous child-process cases cover before acceptance, after acceptance and verified-before-wait; accepted actions are never repeated |
| Budget/normal no-goals lifecycle | Persistent stop, factual result, exact archive bytes, no reused RunId or refunded limits; missing observations remain Blocked |
| Long-run reconstruction | Same terminal counters and state after completing twenty parents |

## Failures and limits retained

R12 corrected its uneven lowest-rule prediction; R13 corrected final training levels; R14 fixed loss of parent purpose after recovery/equip. R15 corrected both long-policy cost predictions42/214 and lowest capacity cooldown74 (two-code deposits cost more). These amendments retain the original failed predictions and do not change scenarios, utility or the zero comparative regression allowance.

Intermittent file-run failures remain unexplained and disclosed in [known limitations](known-limitations.md). Earlier isolated and full repeated passes do not establish a root cause. Exact final verification/revision and diagnostics are in [R15 verification](../openspec/changes/autonomous-quality/verification.md).

Synthetic XP/cooldown and supported catalog subsets do not prove live compatibility, arbitrary recipe feasibility, global optimality or real combat safety. Missing/reconciled fact coverage stays missing. No live actions, scheduler or remote hosting are part of this acceptance.
