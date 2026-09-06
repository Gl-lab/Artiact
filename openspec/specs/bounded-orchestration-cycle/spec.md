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
A cycle invocation SHALL read one current character snapshot, evaluate exactly one top-level `GoalDecision`, and return that exact decision. For `Selected`, it SHALL construct the selected execution goal, decompose it, build its step graph, and execute that graph exactly once. For `Completed` or `Blocked`, it SHALL return without constructing, decomposing, building, or executing a goal. It SHALL NOT contain a hidden fixed-count loop that starts additional goal decisions.

#### Scenario: One successful cycle
- **WHEN** a caller invokes one cycle after successful initialization and evaluation returns `Selected`
- **THEN** character read and decision evaluation each occur once, goal construction, decomposition, step construction, and top-level step execution each occur once, and the exact decision is returned

#### Scenario: One terminal cycle
- **WHEN** a caller invokes one cycle and evaluation returns `Completed` or `Blocked`
- **THEN** character read and decision evaluation each occur once, the exact decision is returned, and no goal construction, decomposition, step construction, or execution occurs

#### Scenario: Repeated worker operation
- **WHEN** the hosted worker receives `Selected`
- **THEN** it explicitly invokes the next single cycle serially

#### Scenario: Terminal worker operation
- **WHEN** the hosted worker receives `Completed` or `Blocked`
- **THEN** it terminates normally without a recovery delay or another cycle

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
The change SHALL preserve existing decomposition order, resource and movement selection, character updates from action responses, and bounded looting behavior. Goal selection SHALL instead follow `GoalDecision`; terminal decisions SHALL perform no execution; and a selected gathering step SHALL enforce the configured mining target and ten-unit free-inventory boundary immediately before every gather.

#### Scenario: Existing gathering plan
- **WHEN** the explainable selector returns `Selected/mining_below_target`
- **THEN** the existing gathering resource and movement behavior remains available, while each gather is authorized by the selected target and inventory boundary

#### Scenario: Terminal gathering decision
- **WHEN** the selector returns `Completed` or `Blocked`
- **THEN** no gathering plan is constructed or executed

#### Scenario: Existing looting-aware crafting plan
- **WHEN** an independently supplied craft target requires one supported loot prerequisite
- **THEN** the existing bounded fight-then-craft behavior remains available and its tests remain passing

### Requirement: In-flight HTTP cancellation is not overstated
This capability SHALL NOT claim cancellation of a game-client HTTP request already in flight unless the implementation also changes the corresponding client contract and verifies that behavior. When an already-started mutating request returns successfully, the returned authoritative character snapshot SHALL be saved before cancellation is propagated. No subsequent cooldown wait, repeated action, or following child action SHALL start after cancellation is observed. The limitation SHALL remain documented if in-flight client cancellation is deferred.

#### Scenario: Deferred client cancellation
- **WHEN** cancellation is requested during a game-client HTTP operation whose contract has no cancellation token
- **THEN** the current request may complete, its successful returned character snapshot is saved, and orchestration starts no subsequent wait or action after cancellation is observed
