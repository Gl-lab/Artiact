## Why

Mining target and inventory guards now work, but resource selection can fail on an empty catalog or missing map, and a selected step repeats gathering without a progression bound or resource reselection. The mock supports only one move and one gather, so it cannot demonstrate autonomous milestone completion.

## What Changes

- Complete the remaining mining vertical slice: resolve a deterministic eligible resource/map pair, invoke each game-client method Move and Gathering at most once per cycle (existing internal HTTP retries and unknown server effects remain outside this bound), and reevaluate authoritative state on the next cycle.
- Add typed terminal reasons for unavailable mining destinations, invalid catalogs/progress snapshots, failed movement postconditions, no progress, and exhausted cycle budget. Preserve target-completion and ten-unit inventory-reserve precedence.
- Apply invalid-catalog decisions only to values returned to the resolver. Client loading/parsing exceptions retain initialization-failure or cycle-recovery behavior; no exception-message classification is added.
- Add validated `MiningProgression:MaxCycles` and `MiningProgression:MaxConsecutiveNoProgress` settings and run-local bounds; retain `GoalSelection:MiningTargetLevel`.
- Add a separate named `mining-progression` mock scenario with repeatable gathering and synthetic level transitions. Preserve every existing `basic-mining` contract.
- Prove completion, resource reselection, bounded failures, cancellation and deterministic replay through the real application orchestration and real clients over TestServer, with an injected cooldown wait for tests.
- Specify and independently verify complete progression action responses, including resource-specific details, actual move destinations, committed character snapshots and cooldowns.
- Inventory pressure remains `Blocked`; automatic crafting, deletion, banking and recovery are deliberately deferred. This is the bounded minimum of Epic 4, not the entire historical coarse epic.

## Capabilities

### New Capabilities

- `bounded-mining-progression`: Resource selection, mining cycle execution, run limits, explanations and end-to-end acceptance.
- `mining-progression-scenario`: An additional deterministic HTTP scenario with repeated gathers and explicit synthetic XP rules.

### Modified Capabilities

- `bounded-orchestration-cycle`: Resolve the final mining decision before execution and preserve non-mining strategy semantics while replacing mining's internal gather loop with bounded cycles.
- `explainable-mining-goal-selection`: Extend final decision reasons and execution/explanation rules. This capability is supplied by the completed predecessor change and must be synchronized before this delta is applied.
- `deterministic-mock-scenario`: Preserve basic-mining literals while extending named reset, catalog/trace selection and scenario-specific lifecycle rules. Supplied by the completed mock predecessor and synchronized before applying this delta.

## Impact

- `Artiact`: ActionService, mining StepBuilder path, application-local decision/plan models, focused resource resolver, progression state/settings, cooldown seam and Program DI; worker retains Selected/terminal branching.
- `Artiact.MockService`: scenario selection/loading, store transitions and an additional fixture; controllers retain the existing HTTP routes and JSON envelopes.
- `Artiact.Tests` and `Artiact.MockService.Tests`: focused behavior and socket-free full progression tests. No new package is expected.
- `Artiact.Contracts`: no change planned; an application-local resolved GatheringGoal subtype carries immutable mining destination data. No API DTO or IGameClient route/signature change is planned.
- Update affected docs and MockService AGENTS.md during implementation. Do not refresh tracked caches or change client retries.

Dependency: implementation follows `add-explainable-mining-goal-selection` at base `098494da5cf4a5ff9a7af451c4268abc9068cce8` and `add-minimal-deterministic-mock-service`. Both have completed task lists but remain unarchived. Their specs must be synchronized before applying/archive of this change; do not apply the stale base orchestration text over their implemented behavior. Preparing this proposal does not archive those changes or authorize implementation.
