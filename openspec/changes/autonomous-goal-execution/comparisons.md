# R12 HTTP protocol v1 — preregistration, 2026-09-08

R11 fixtures are transferred to the scripted mock with equivalent map IDs (mine=4, wood=5, iron=7). Skill threshold=26, accepted gathering grants 13 XP and one item, cooldown=5; movement=7; capacity=100. This is synthetic XP, not a live claim. New/bank starts on wood, uneven starts on mine with mining=9; inaccessible starts on wood with mining=9 and a locked iron map. Bank holds 10 ore in the bank case. Bounds: 20 actions, 300 seconds, 40 decisions. No banking actions are needed.

Baselines clarified before execution: fixed portfolio contains mining and woodcutting next-level goals at equal value, using the existing full-path selector; lowest rule executes the next level of the lowest supported accessible skill, ordinal ties, retaining that goal until complete. A raw list alone does not impose execution order on the fixed selector. Each strategy is evaluated until the independently specified useful result or the shared limit. This clarification replaces the ambiguous R11 phrase 'fixed mining then woodcutting'; the actual portfolio selector has never promised list order. Preserve all results.

| Case | Useful result | Auto ceiling actions/cooldown/unproductive moves | Fixed expected | Lowest expected |
|---|---|---|---|---|
| New | woodcutting 2 | 2 / 10 / 0 | 2 / 10 / 0 | 6 / 34 / 2 |
| Bank | woodcutting 2, bank ore remains 10 | 2 / 10 / 0 | 2 / 10 / 0 | 6 / 34 / 2 |
| Uneven | mining 10, unlock iron | 2 / 10 / 0 | 2 / 10 / 0 | no mining-10 result in 20 actions while training wood |
| Inaccessible | woodcutting 2, no use of locked map | 2 / 10 / 0 | 2 / 10 / 0 | 2 / 10 / 0 |

The ordinary zero-regression ceiling applies relative to each successful baseline. New/bank must supply the command win; uneven failure remains recorded rather than converted to a fabricated comparison time. Every result must replay exactly. Two-milestone lifecycle acceptance separately uses the new fixture with 6 actions, records wood levels 2 and 3 after four gathers (20 confirmed cooldown seconds), and stops when the remaining budget cannot cover the next estimated milestone.

## Amendment 1: baseline prediction error

First comparison run passed six of seven flow tests and failed the uneven baseline outcome assertion: lowest did reach mining 10 at exactly 20 actions / 104 cooldown seconds. The preregistration overlooked the ordinal tie at woodcutting 9: it switches to mining before training woodcutting 10. Keep the original table above as the failed prediction. Correct expectation: lowest achieves mining 10 at 20 / 104 / 2; no fixture, autonomous formula, useful-result requirement or regression ceiling changes. Repeat every strategy in all four cases and replay after this amendment.

## Observed results

All four cases and all three strategies were rerun and replayed after Amendment 1. Auto and fixed each achieved the useful outcome in 2 commands / 10 confirmed cooldown seconds / 0 moves. Lowest: new and bank 6 / 34 / 2, uneven 20 / 104 / 2, inaccessible 2 / 10 / 0. Bank ore remained 10. The original useful-result and zero-regression gates pass: three command/cooldown wins over lowest and one tie; four ties with fixed. No aggregate cross-skill XP score substitutes for these outcomes. Replay compares complete final mock state and measured action/cooldown/move tuples, excluding wall-clock instrumentation.
