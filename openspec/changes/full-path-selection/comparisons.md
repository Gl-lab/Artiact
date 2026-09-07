# Fixed/adaptive comparisons — 2026-09-07

Reproduce synthetic comparisons with `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~FullPathSelectionTests`. Each starts at 0 XP with target 40, identical 100-action/100-decision/3600-second budget, same two candidates and initial estimated 10 XP/one second. Fixed uses ordinal tie a; adaptive uses FullPaths, UnknownMultiplier 2, SwitchRatio 1.1. Every run reaches the same threshold (last row reaches 41 adaptive due to atomic XP batches). Repeat adaptive runs match exactly, including per-tick reconstruction.

| Actual a seconds/XP; b seconds/XP | Fixed actions / cooldown / active seconds | Adaptive actions / cooldown / active seconds | Adaptive switches | Result |
|---|---|---|---:|---|
| 12/10; 3/10 | 4 / 48 / 52 | 4 / 21 / 25 | 1 | Win |
| 2/10; 2/10 | 4 / 8 / 12 | 4 / 8 / 12 | 0 | Tie |
| 3/10; 20/10 | 4 / 12 / 16 | 4 / 29 / 33 | 2 | Loss: exploring b costs more than returning to a saves |
| 12/1; 3/10 | 40 / 480 / 520 | 5 / 24 / 29 | 1 | Unequal observed XP changes required action count |

Active seconds use a deterministic clock: two observations of 0.25 s plus dispatch 0.5 s and actual wait per verified action. Total run elapsed additionally includes final observation; production wall time is not inferred from this synthetic clock. Preparation/material consumption is zero in these isolated comparisons and explicitly asserted. Both policies use the same declared value/goal.

Real-client TestServer comparisons: `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~FullPathMode`. Fixed and adaptive start from a fresh named reset, use the same goal and limits (100 actions/100 decisions/60 no-progress/3600 seconds), and recreate the session every tick. Both reach their targets with the same oracles:

| Scenario / target | Actions / cooldown seconds, both policies | Verified consumed inputs, both policies |
|---|---|---|
| item-production / tool 1 | 6 / 32 | ore 2, bar 1 |
| capacity-training / tool 1 | 9 / 50 | ore 2, bar 1; two training crafts first |
| consumable-production / tool 1 | 23 / 127 | ore 2, bar 1, fish 4, meal 2 |
| autonomous-weapon / combat 3 | 20 / 120 | ore 2, feather 2; two retained training bars |
| autonomous-shield + food / combat 3 | 38 / 221 | fish 6, meal 4, feather 2 |

These routes have no beneficial alternative, so no gain is claimed. Both combat paths verify total combat XP 20. All verified actions retain facts/wait completion/context (context only in full mode), all bank transfers have zero combined-stock delta, and input maps are exact independent assertions. System-clock HTTP/processing times are available in run-result but are not stable speed benchmarks; no artificial speedup is asserted for no-delay TestServer runs. Route switches are exposed by the same RunPerformance aggregation tested in the synthetic comparisons.

Additional boundaries: equipment/level/catalog context invalidation, cost retained after fast learned work, lost response/reconciliation without measurements, unreachable later workshop before any action, exact XP across one level and unknown XP across multiple levels, facts saved before interrupted wait, legacy result coverage and R7–R9 regression oracles.
