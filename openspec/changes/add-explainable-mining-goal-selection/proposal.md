## Why

`GoalService` currently ignores character state and always returns `GatheringGoal(20)`. The worker therefore cannot explain why mining was selected, recognize that the configured milestone is complete, or stop safely when inventory pressure makes another gather unsafe.

A small explainable selector is required before the complete mining progression slice. It must make one deterministic decision from one character snapshot without introducing a generic optimizer or expanding the MockService.

## What Changes

- Add an explicit `GoalDecision` with `Selected`, `Completed`, and `Blocked` status, a stable reason code, typed decision facts, and an immutable selected-goal type; create the mutable execution goal only after selection.
- Add one validated operator setting, `GoalSelection:MiningTargetLevel`, with tracked default `20`.
- Select immutable `GoalType.Gathering` only while mining is below the target and at least ten inventory units remain free; orchestration then creates a private `GatheringGoal(target)` execution graph.
- Return `Completed` at or above the target without decomposing, building, or executing a goal.
- Return `Blocked` without fallback when a below-target snapshot is malformed or has fewer than ten free inventory units.
- Make the selected gathering step check both the configured target and the same ten-unit free-inventory boundary immediately before every gather, including after movement and after each authoritative gather response.
- Make `ActionService` expose the decision and attach deterministic decision fields to logs/activity telemetry.
- Make the background worker terminate normally on `Completed` or `Blocked` instead of spinning an empty decision loop.
- Add focused selector, orchestration, worker, configuration, and observability tests; update domain/development documentation with the exact boundary behavior.

## Capabilities

### New Capabilities

- `explainable-mining-goal-selection`: One character snapshot and one configured mining milestone produce a deterministic, observable, fail-closed goal decision.

### Modified Capabilities

- `bounded-orchestration-cycle`: A cycle evaluates exactly one typed goal decision; only `Selected` reaches decomposition/build/execution, while `Completed` and `Blocked` return without downstream work and terminate autonomous repetition normally.

## Impact

- `Artiact/Services/GoalService.cs`, `IGoalService.cs`, `ActionService.cs`, `IActionService.cs`, `StepBuilder.cs`, and `ArtiactBackgroundService.cs`.
- A minimal immutable decision/settings model in the owning application layer and one shared DI-registration extension used by `Artiact/Program.cs` and configuration tests, plus tracked non-secret configuration.
- Focused tests under `Artiact.Tests/Services/` and configuration tests using an in-memory service collection rather than starting the host.
- `docs/README.md`, `docs/domain-model.md`, `docs/architecture.md`, `docs/development.md`, and `docs/known-limitations.md`.
- No resource ranking, movement planning, crafting repair, inventory disposal, bank, market, combat, weighted scoring, persistence, MockService expansion, production host smoke, credentials, or production API access.
