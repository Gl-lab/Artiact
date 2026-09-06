# Viability alternatives and experiment findings

Research date: 2026-09-06. Inputs and independent oracles are in [the protocol](experiments.md); command/results evidence is in [the change evidence](../../../openspec/changes/research-combat-equipment-progression/execution-evidence.md).

| Criterion | Conservative local bound | Official simulator | Observed-outcome calibration |
|---|---|---|---|
| Inputs | Complete normalized player/opponent stats and supported mechanics | Fake character loadouts, monster, iteration count, membership | Versioned full pre-state, opponent, outcome, post-state and sampling policy |
| Offline execution | Implemented and tested | Not called; documented interface only | No matched corpus; synthetic reasoning only |
| Uncertainty | Worst incoming crit/order, no outgoing crit credit; Unknown outside subset | Finite sampled outcomes, hidden server model/version changes | Selection bias, stale equipment/content, rare failures |
| False-safe risk | Low only within explicit mathematical assumptions; normalization/effect omissions invalidate proof | All sampled wins still permit future loss | Same past observations can precede different next outcomes |
| Explanation | Damage stages, exchanges, loss bound, rejection reason | Logs and aggregate win rate need interpretation | Confidence/coverage depends on collected data |
| Cost | Small arithmetic kernel plus boundary maintenance | Access/network dependence, contract drift | Data provenance/storage and ongoing coverage work |
| Decision | Use for narrow offline Epic 6 design | Optional later calibration evidence | Diagnostic veto/calibration later; not primary authorization |

## Same-fixture comparison

| Fixture | Local experiment result | Official simulation | Observation-based inference |
|---|---|---|---|
| safe, HP20/attack10 versus HP20/attack3 | Safe, two exchanges, loss 6 | Unmeasured; no fabricated win rate | No actual evidence; cannot establish live safety |
| HP7 / HP6 boundary | Safe with 1 / Unsafe with 0 | Unmeasured | Prior HP20 wins do not answer either boundary |
| critical-bound, HP10 with 1% enemy crit | Unsafe, worst loss 10 | Could sample only ordinary hits; still unmeasured | Identical ordinary-hit history is compatible with a later lethal critical sequence |
| tied initiative | Safe fixture remains Safe; worst enemy-first charge | Unmeasured | Previous favorable order cannot authorize next fight |
| unsupported effect/missing data | Unknown, zero commands | Optional source of investigation, not fallback execution | No transferable inference from effect-free fights |
| equipment attack10 at level1 versus attack4 at level9 | Select level1, loss6 versus15 | Unmeasured | Item level is an insufficient covariate |

The critical counterexample does not require a fitted stochastic model: both an all-ordinary past and a next critical sequence are compatible with nonzero enemy critical chance. Observed wins alone cannot provide the chosen all-supported-outcomes survival criterion. We do not assign invented empirical precision or a numeric live win rate. No random experiment is presented; every executable fixture is deterministic.

## What the transition experiments establish

The golden script reaches level 2 in five decisions with four commands and 29 virtual seconds. Independent assertions check full final state and ordered decisions; a second fresh run must match. Each failure fixture also replays from fresh objects. Swapping a pre-owned weapon adds two commands and six virtual seconds with a sufficiently large no-progress limit. The default smaller limit intentionally blocks a longer prerequisite chain instead of refunding attempts.

Tests cover exact HP/rounding, turn exhaustion, unsupported inputs, target/config/state validation, recovery/cycle/fight/no-progress limits, defeat, failed gear changes, wrong movement, lost response and cancellation. A self-review regression exposed equipment dispatch before validating unknown current stats; a failing test reproduced two unwanted commands, and the guard now rejects that case before any mutation.

These are tests of a research model and scripted controller. They do not prove GameClient payload compatibility, live combat viability, a full combat emulator or transport exactly-once behavior. The payload probes independently demonstrate existing compatibility gaps while leaving production behavior unchanged.
