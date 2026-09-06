## MODIFIED Requirements

### Requirement: Mining selection returns only constructively valid deterministic decisions
For one configured target and one character snapshot, the selector SHALL return exactly one immutable `GoalDecision`. `GoalDecisionStatus` SHALL contain only `Selected`, `Completed`, and `Blocked`. The pure selector SHALL emit only the following existing `GoalDecisionReason` values: `MiningBelowTarget`, `MiningTargetReached`, `InvalidGoalPolicy`, `InvalidCharacterSnapshot`, `InvalidInventorySnapshot`, and `InventoryPressure`; an exhaustive mapping SHALL expose the stable respective codes `mining_below_target`, `mining_target_reached`, `invalid_goal_policy`, `invalid_character_snapshot`, `invalid_inventory_snapshot`, and `inventory_pressure`.

`CurrentMiningLevel` SHALL be nullable when no character was observed. `InventoryCapacity`, `InventoryUsed`, and `InventoryFree` SHALL be nullable as a group. Every decision SHALL include the supplied `MiningTargetLevel`, `RequiredFreeInventory=10`, and nullable `SelectedGoalType` using the existing `GoalType` enum. The decision SHALL retain no mutable `Goal`. The type SHALL expose no public constructor/setter and SHALL provide only factories that reject invalid enums or combinations.

The existing valid combinations SHALL remain:

- `Selected/MiningBelowTarget`: non-negative current level below target; consistent non-null inventory facts with free at least 10; `SelectedGoalType=GoalType.Gathering`.
- `Completed/MiningTargetReached`: non-negative current level at least target; all inventory facts and selected goal type null.
- `Blocked/InvalidGoalPolicy`: current level, inventory facts, and selected goal type all null because policy validation precedes character inspection.
- `Blocked/InvalidCharacterSnapshot`: inventory facts and selected goal type null; current level null for absent character or the observed negative value.
- `Blocked/InvalidInventorySnapshot`: valid non-negative below-target current level; inventory facts and selected goal type null.
- `Blocked/InventoryPressure`: valid non-negative below-target current level; consistent inventory facts with free below 10; selected goal type null.

The same target and equal relevant snapshot values SHALL produce field-equal decisions and equivalent goal data without mutation, external I/O, timestamps, randomness, or generic scoring.

#### Scenario: Invalid selected combination is unconstructable
- **WHEN** code attempts to create Selected with another reason, missing/non-Gathering selected type, contradictory target/inventory facts, or free inventory below ten
- **THEN** decision construction fails before the value can reach orchestration

#### Scenario: Repeated evaluation is stable
- **WHEN** equal relevant snapshots are evaluated under the same target
- **THEN** every decision field, reason code, and selected goal field is equal

The final progression stage SHALL additionally allow Blocked reasons `InvalidMiningProgress`, `MiningDestinationNotReached`, `MiningNoProgress`, `MiningCycleLimit`, `InvalidMiningCatalog` and `NoMiningDestination`, with exact codes `invalid_mining_progress`, `mining_destination_not_reached`, `mining_no_progress`, `mining_cycle_limit`, `invalid_mining_catalog` and `no_mining_destination`. Each SHALL require positive target, nonnegative current level below target, null inventory facts and null selected goal type. The factory SHALL reject all other combinations; these reasons SHALL NOT be emitted by the pure selector. Final decisions additionally depend on run state and catalog inputs; equal complete inputs SHALL remain deterministic.

#### Scenario: Progression reason cannot authorize actions
- **WHEN** construction combines a progression-only reason with Selected or inventory facts
- **THEN** construction fails before orchestration can execute the value

### Requirement: Only Selected decisions reach goal execution
The cycle SHALL evaluate the pure selector from one initial character read after pre-cancellation, then finalize a Selected result through progression guards and destination resolution. Pure Completed/Blocked SHALL return unchanged without catalog I/O or execution. A final Blocked result SHALL allow prior read-only catalog resolution but SHALL perform no goal construction, decomposition, building or mutation. Final Selected SHALL construct a fresh resolved gathering goal, decompose, build and execute it once, and return the exact final decision explained before execution. Graph mutation SHALL NOT alter that decision. Expected terminal results SHALL NOT throw; infrastructure exceptions SHALL retain existing propagation/recovery, with attempted selected cycles consuming progression budget.

#### Scenario: Blocked cycle performs no downstream work
- **WHEN** evaluation returns `Blocked/inventory_pressure`
- **THEN** the same decision is returned with zero decomposition, building, step, and game-action calls

#### Scenario: Selected cycle preserves the existing path
- **WHEN** evaluation returns `Selected/mining_below_target` with `SelectedGoalType=GoalType.Gathering` and target `20`, progression guards pass and destination resolution succeeds
- **THEN** a fresh `GatheringGoal(20)` is decomposed, built, and executed once using the resolved destination, and the final immutable decision is returned unchanged

#### Scenario: Pre-cancellation selects nothing
- **WHEN** cancellation exists before cycle invocation
- **THEN** no character is read, no decision is evaluated, and no downstream work starts

### Requirement: Every gather obeys both live decision boundaries
Every selected mining execution SHALL use the shared inventory rule and selected target immediately before movement and gathering, including after any authoritative move response. Gathering SHALL require nonnegative below-target mining, valid free inventory at least ten, valid progress facts, eligible resource and the resolved coordinates. Malformed values SHALL suppress actions without throwing. A cycle SHALL invoke the game-client Gathering method at most once; live guards and action counts in this requirement apply to application method invocations, not internal HTTP retries or exactly-once server effects; further work SHALL require another decision cycle and resource reselection. A level change after movement SHALL suppress gathering until reselection. The following previous live-boundary scenarios remain required.

#### Scenario: Movement invalidates first-gather authorization
- **WHEN** selection authorized gathering but the authoritative move response reaches the target or leaves fewer than ten free inventory units
- **THEN** the moved character is retained, no gather starts, the selected cycle returns normally, and the next cycle emits Completed or the applicable Blocked and terminates without recovery delay

#### Scenario: Crossing from ten to nine free units stops the cycle
- **WHEN** selection begins with free `10` and the first gather response has free `9`
- **THEN** exactly one gather occurs and no second gather starts

#### Scenario: Reaching target stops the cycle
- **WHEN** the first gather response raises mining level to the selected target
- **THEN** exactly one gather occurs and no second gather starts

#### Scenario: Malformed returned inventory does not authorize another gather
- **WHEN** a successful gather response contains malformed inventory data
- **THEN** the authoritative character response is retained, the selected cycle returns normally after one gather, the next worker cycle returns `Blocked/invalid_inventory_snapshot`, exactly two decision events and two cycles occur, no recovery delay runs, and no second gather starts

#### Scenario: Negative returned mining level does not authorize another gather
- **WHEN** a successful gather response contains mining level `-1` with otherwise valid free capacity
- **THEN** the authoritative response is retained, the selected cycle returns normally after one gather, the next worker cycle returns `Blocked/invalid_character_snapshot`, exactly two decision events and two cycles occur, no recovery delay runs, and no second gather starts

### Requirement: One bounded structured explanation is observable
ActionService SHALL emit exactly one structured information decision event per finalized cycle and tag its existing activity from the same immutable decision. Base fields are `goal.decision.status`, `goal.decision.reason`, and `goal.mining.target_level`; `goal.mining.current_level` SHALL appear only when non-null. Inventory fields `goal.inventory.capacity`, `goal.inventory.used`, `goal.inventory.free`, and `goal.inventory.required_free` SHALL appear only when all inventory facts are non-null. Completed and invalid-snapshot decisions SHALL have no inventory fields; a null-character decision SHALL also omit current level.

No inventory contents, account, credentials, authorization data, serialized character, timestamp, or random identifier SHALL be included. Logging/listener/exporter presence MUST NOT affect the returned decision or execution.

#### Scenario: Inventory-pressure explanation has exact facts once
- **WHEN** valid below-target inventory has free `9`
- **THEN** one decision event and activity contain matching status/reason/level/capacity/used/free/required values and no inventory contents

#### Scenario: Completed explanation omits inventory
- **WHEN** mining has reached target with otherwise valid inventory
- **THEN** one decision event and activity contain status/reason/current/target only and omit every inventory field

Final Selected SHALL additionally emit `goal.mining.resource_code`, `goal.mining.resource_level`, `goal.mining.destination_x` and `goal.mining.destination_y` from the immutable resolved destination. Final Selected and progression-only Blocked SHALL also emit `goal.mining.attempted_cycles`, `goal.mining.max_cycles`, `goal.mining.consecutive_no_progress` and `goal.mining.max_no_progress` from the pre-execution run state after this cycle's budget reservation, if any. Other terminal decisions SHALL omit these added fields. The same values SHALL appear in the activity; the preliminary pure Selected SHALL not emit another decision event. An infrastructure failure before finalization SHALL propagate through existing error telemetry without a fabricated final decision.

#### Scenario: Destination failure has one truthful explanation
- **WHEN** the pure selector is Selected but no eligible destination exists
- **THEN** one Blocked/no_mining_destination event is emitted with limits/counters, no selected resource fields and no preceding Selected event

### Requirement: Scope remains narrow and fail-closed
The mining progression extension SHALL permit deterministic resource/movement selection, finite mining cycles, an additional mock scenario and an injected mining cooldown wait. It SHALL preserve independently supplied craft/loot behavior and SHALL NOT add automatic inventory remediation, generic candidate scoring, a generic plan state machine, persistence, new packages, credentials, production action access or live/main-host verification.

#### Scenario: Selector does not claim an unproven prerequisite
- **WHEN** mining remains below target but free inventory is below ten
- **THEN** the result is `Blocked`, not selected crafting, deletion, recycle, bank, or gathering
