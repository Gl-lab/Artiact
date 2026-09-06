# bounded-orchestration-cycle Specification

## Purpose
Provide a deterministic, cancellable unit of orchestration so tests and operators can invoke one top-level Artiact decision without an implicit multi-cycle batch.

## Requirements

### Requirement: Initialization is explicit and precedes execution
The orchestration service SHALL expose asynchronous initialization separately from cycle execution. A worker SHALL complete initialization successfully before it starts the first cycle.

#### Scenario: Successful initialization
- **WHEN** the orchestration service is initialized
- **THEN** reference data is warmed and the configured character snapshot is loaded before any goal is selected

#### Scenario: Initialization failure
- **WHEN** cache warm-up or initial character loading fails
- **THEN** no goal cycle is started and the failure is propagated to the caller

### Requirement: One invocation executes one top-level cycle
A cycle invocation SHALL select one top-level goal, decompose that goal, build its step graph, and execute that graph exactly once. It SHALL NOT contain a hidden fixed-count loop that starts additional goal cycles.

#### Scenario: One successful cycle
- **WHEN** a caller invokes one cycle after successful initialization
- **THEN** goal selection, decomposition, step construction, and top-level step execution each occur once

#### Scenario: Repeated worker operation
- **WHEN** the hosted worker needs continuous operation
- **THEN** the worker explicitly invokes successive single cycles rather than relying on a multi-cycle orchestration method

### Requirement: Cancellation prevents new work
The orchestration path SHALL accept a cancellation token. Cancellation SHALL be observed before initialization, before selecting a new goal cycle, during orchestration-owned recovery delays, and during step-owned cooldown delays introduced or modified by this change.

#### Scenario: Cancellation before a cycle
- **WHEN** cancellation is requested before a cycle begins
- **THEN** no goal is selected and no game action is started

#### Scenario: Cancellation during worker recovery
- **WHEN** cancellation is requested while the worker is waiting after a recoverable failure
- **THEN** the wait ends and the worker exits without starting another cycle

#### Scenario: Cancellation during a cooldown wait
- **WHEN** cancellation is requested while a step is waiting for the returned cooldown
- **THEN** the wait ends and no subsequent action in that step graph is started

### Requirement: Telemetry is non-blocking
The absence of an active tracing listener SHALL NOT prevent initialization or cycle execution. When an activity exists, orchestration failures SHALL still mark it as failed.

#### Scenario: No activity listener
- **WHEN** a cycle runs without any listener for the configured activity source
- **THEN** the cycle executes normally without a telemetry-related exception

#### Scenario: Instrumented cycle failure
- **WHEN** an activity exists and cycle execution fails
- **THEN** the activity records an error status and the original failure is propagated

### Requirement: Existing strategy semantics remain compatible
The change SHALL preserve the existing goal selection, decomposition order, step construction, character updates from action responses, and bounded looting behavior except for removing the hidden five-cycle batch.

#### Scenario: Existing gathering plan
- **WHEN** the current fixed gathering policy is executed through one cycle
- **THEN** it produces the same single-cycle goal and step behavior as one iteration of the previous batch

#### Scenario: Existing looting-aware crafting plan
- **WHEN** a craft target requires one supported loot prerequisite
- **THEN** the existing bounded fight-then-craft behavior remains available and its tests remain passing

### Requirement: In-flight HTTP cancellation is not overstated
This capability SHALL NOT claim cancellation of a game-client HTTP request already in flight unless the implementation also changes the corresponding client contract and verifies that behavior. When an already-started mutating request returns successfully, the returned authoritative character snapshot SHALL be saved before cancellation is propagated. No subsequent cooldown wait, repeated action, or following child action SHALL start after cancellation is observed. The limitation SHALL remain documented if in-flight client cancellation is deferred.

#### Scenario: Deferred client cancellation
- **WHEN** cancellation is requested during a game-client HTTP operation whose contract has no cancellation token
- **THEN** the current request may complete, its successful returned character snapshot is saved, and orchestration starts no subsequent wait or action after cancellation is observed
