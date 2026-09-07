# Measured portfolio selection

R5 is opt-in: `Portfolio:MeasuredSelection=true`, `UnknownMultiplier=2` and `SwitchRatio=1.1` by default. Multipliers must be between 1 and 10. Fixed selection remains the default. Measured gathering exposes each catalog resource for a configured skill as a separate candidate. `MonsterAlternatives` optionally adds distinct explicit combat opponents (not combined with PrepareEquipment); `Items` already supplies distinct production goals. Every alternative retains existing safety/access/stock checks.

Only verified positive returned cooldowns produce timing samples. The run journals cumulative seconds, sample count and number of verified productive commands by candidate/action kind. These counts are observed successful steps, not a prediction of XP quantity or live XP/hour. Lost/ambiguous replies do not fabricate duration samples. The journal retains samples and incumbent across restart; policy changes fail the existing identity guard.

Candidate output includes `EstimateSource` and `Samples` with action/travel/recovery components. The current command component uses its sample mean when available; unknown components use the configured estimate multiplied by UnknownMultiplier. Production additionally includes remaining recipe work, required gathering/withdrawals and a conservative movement allowance. Loot estimates are assumptions with a finite execution budget, not guaranteed acquisition times. These estimates do not model every live modifier or globally optimize an entire craft route.

The best feasible score wins unless the incumbent remains feasible and the competitor fails to exceed its score times SwitchRatio. This limits unnecessary switching while allowing a clearly better option. Ordinal IDs break initial ties. It is not an exploration optimizer and does not guarantee discovering the global best alternative.

Independent synthetic four-action comparisons (one completed goal in every run):

| Slow/fast action seconds | Fixed total | Measured total | Actions | Replay |
|---|---:|---:|---:|---|
| 12 / 3 | 48 | 21 | 4 | Identical |
| 9 / 2 | 36 | 15 | 4 | Identical |
| 20 / 5 | 80 | 35 | 4 | Identical |

These fixtures isolate ranking from game mechanics. Additional TestServer coverage verifies multi-resource observation/preflight/reply fingerprints. R3/R4 scenarios remain regression checks; the table is not a claim of measured bank/craft efficiency or optimal live progression. [Evidence](../openspec/changes/measured-selection/execution-evidence.md).
