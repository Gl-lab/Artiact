# explainable-mining-goal-selection Specification

## Purpose
Preserve the implemented explainable-mining-goal-selection behavior from its completed predecessor change.

## Requirements

### Requirement: Mining selection returns only constructively valid deterministic decisions
For one configured target and one character snapshot, the selector SHALL return exactly one immutable `GoalDecision`. `GoalDecisionStatus` SHALL contain only `Selected`, `Completed`, and `Blocked`. `GoalDecisionReason` SHALL contain only `MiningBelowTarget`, `MiningTargetReached`, `InvalidGoalPolicy`, `InvalidCharacterSnapshot`, `InvalidInventorySnapshot`, and `InventoryPressure`; an exhaustive mapping SHALL expose the stable respective codes `mining_below_target`, `mining_target_reached`, `invalid_goal_policy`, `invalid_character_snapshot`, `invalid_inventory_snapshot`, and `inventory_pressure`.

`CurrentMiningLevel` SHALL be nullable when no character was observed. `InventoryCapacity`, `InventoryUsed`, and `InventoryFree` SHALL be nullable as a group. Every decision SHALL include the supplied `MiningTargetLevel`, `RequiredFreeInventory=10`, and nullable `SelectedGoalType` using the existing `GoalType` enum. The decision SHALL retain no mutable `Goal`. The type SHALL expose no public constructor/setter and SHALL provide only factories that reject invalid enums or combinations.

Valid combinations are exactly:

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

### Requirement: Selection uses exact policy and snapshot precedence
Evaluation precedence SHALL be:

1. target less than or equal to zero -> `Blocked/invalid_goal_policy`;
2. null character or negative mining level -> `Blocked/invalid_character_snapshot`;
3. mining level at least target -> `Completed/mining_target_reached` with all inventory facts null and no inventory access;
4. below target with null inventory list, any null inventory element, negative capacity, any negative quantity, blank/null item code with positive quantity, checked-sum overflow, or used greater than capacity -> `Blocked/invalid_inventory_snapshot` with all inventory facts null;
5. valid free capacity below ten -> `Blocked/inventory_pressure` with exact capacity/used/free facts;
6. otherwise -> `Selected/mining_below_target` with exact facts and `SelectedGoalType=GoalType.Gathering`.

Every inventory check, including live gather authorization, SHALL use one shared rule: zero-quantity slots contribute zero regardless of code; blank/null code with positive quantity is malformed; positive quantities with nonblank codes are summed in checked arithmetic.

#### Scenario: Exactly ten free units selects gathering
- **WHEN** target is `20`, mining is `19`, capacity is `20`, and non-negative quantities sum to `10`
- **THEN** the result is `Selected/mining_below_target`, facts are current `19`, target `20`, used `10`, free `10`, required `10`, and selected goal type is `GoalType.Gathering`

#### Scenario: Nine free units blocks
- **WHEN** target is `20`, mining is `19`, capacity is `20`, and quantities sum to `11`
- **THEN** the result is `Blocked/inventory_pressure`, reports free `9`, contains no goal, and selects no fallback

#### Scenario: Null inventory entry blocks
- **WHEN** a below-target inventory list contains a null element
- **THEN** the result is `Blocked/invalid_inventory_snapshot` with all inventory facts null and no goal

#### Scenario: Exact or exceeded target completes independently of inventory
- **WHEN** target is `20`, mining is `20` or `21`, and inventory is valid, malformed, or null
- **THEN** the result is `Completed/mining_target_reached`, current level is observed, all inventory facts are null, no inventory tags are emitted, and no goal exists

#### Scenario: Null character does not fabricate a level
- **WHEN** a valid target is evaluated with no character
- **THEN** the result is `Blocked/invalid_character_snapshot`, current level and all inventory facts are null, and no goal exists

### Requirement: Configuration is validated through production registration
The tracked non-secret configuration SHALL set `GoalSelection:MiningTargetLevel=20`. One production `AddGoalSelection(IServiceCollection, IConfiguration)` registration extension SHALL bind this section, register `IGoalService`, validate a strictly positive integer target on startup, and be called by `Program`. Safe tests SHALL invoke the same extension without registering/starting the hosted worker. Absent, non-integer, zero, or negative values MUST fail option validation before autonomous initialization, and no hard-coded fallback target may be substituted.

#### Scenario: Production registration rejects invalid target
- **WHEN** the shared registration is built with absent, non-integer, zero, or negative target configuration and option startup validation is evaluated
- **THEN** validation fails and no goal or game action is selected

### Requirement: Interface migration remains compiling and RED is behavioral
Tests SHALL first express the desired decision API. Their initial missing-type compilation failure is setup evidence, not RED. The smallest compiling decision/enums/factory and `Evaluate(Character?)` skeleton SHALL then be added, with policy behavior still throwing `NotImplementedException`, while current `GetGoal(ICharacterService)` remains temporarily. Selector RED SHALL be recorded only after tests compile and fail on missing policy behavior, then selector tests SHALL reach GREEN. Before orchestration RED, a compile-only skeleton SHALL change `ExecuteCycleAsync` and all callers/test doubles to `Task<GoalDecision>` while its body deliberately throws `NotImplementedException` after the existing pre-cancellation check and executes no old goal. Orchestration tests SHALL then compile and fail on that missing behavior. Only after this behavioral RED may ActionService call `Evaluate` and branch; `GetGoal` SHALL then be removed after a repository search proves no callers remain.

#### Scenario: Orchestration skeleton exposes behavioral RED
- **WHEN** the return signature and test doubles compile but ActionService still has its deliberate missing-behavior throw
- **THEN** a test expecting one `Evaluate` call and the exact decision fails behaviorally before decision branching is implemented

### Requirement: Only Selected decisions reach goal execution
`IActionService.ExecuteCycleAsync` SHALL return the exact valid `GoalDecision` produced from one character snapshot read once after the pre-cancellation check. For `Selected`, ActionService SHALL construct one fresh mutable `GatheringGoal(decision.MiningTargetLevel)` from `SelectedGoalType=GoalType.Gathering`, then decompose, build, and execute that private graph once; graph mutation MUST NOT alter the returned decision. `Completed` and `Blocked` SHALL return without decomposition, step building, step execution, or game-client actions. Expected terminal decisions MUST NOT throw. Infrastructure/implementation exceptions SHALL preserve existing propagation and worker recovery behavior.

#### Scenario: Blocked cycle performs no downstream work
- **WHEN** evaluation returns `Blocked/inventory_pressure`
- **THEN** the same decision is returned with zero decomposition, building, step, and game-action calls

#### Scenario: Selected cycle preserves the existing path
- **WHEN** evaluation returns `Selected/mining_below_target` with `SelectedGoalType=GoalType.Gathering` and target `20`
- **THEN** a fresh `GatheringGoal(20)` is decomposed, built, and executed once and the same immutable decision is returned unchanged

#### Scenario: Pre-cancellation selects nothing
- **WHEN** cancellation exists before cycle invocation
- **THEN** no character is read, no decision is evaluated, and no downstream work starts

### Requirement: Every gather obeys both live decision boundaries
For a selected `GatheringGoal`, `StepBuilder` SHALL evaluate one shared live predicate immediately before every gather against the latest `CharacterService` snapshot: once through an outer condition before the first gather, including after any move response, and again as the repeat predicate after each gather response. It SHALL use the exact shared inventory rule defined for selection and return true only when mining is non-negative and below `GatheringGoal.TargetLevel`, and checked valid free inventory is at least ten. A negative mining level or any malformed inventory, including blank/null code with positive quantity, SHALL return false without throwing. Resource candidate and movement selection SHALL remain unchanged.

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

### Requirement: Terminal decisions stop autonomous repetition normally
The worker SHALL continue its serial loop only after `Selected`. `Completed` or `Blocked` SHALL return normally after one cycle, invoke no recovery delay, and cause no second evaluation of the unchanged snapshot. ActionService SHALL be the sole authoritative decision-log producer; the worker SHALL emit no duplicate decision event.

#### Scenario: Completed stops after one cycle
- **WHEN** the first cycle returns `Completed/mining_target_reached`
- **THEN** exactly one cycle runs, no recovery delay runs, and the worker terminates normally

#### Scenario: Blocked stops after one cycle
- **WHEN** the first cycle returns `Blocked/inventory_pressure`
- **THEN** exactly one cycle runs, no recovery delay runs, and the worker terminates normally

### Requirement: One bounded structured explanation is observable
ActionService SHALL emit exactly one structured information decision event per evaluated cycle and tag its existing activity from the same immutable decision. Exact fields are `goal.decision.status`, `goal.decision.reason`, and `goal.mining.target_level`; `goal.mining.current_level` SHALL appear only when non-null. Inventory fields `goal.inventory.capacity`, `goal.inventory.used`, `goal.inventory.free`, and `goal.inventory.required_free` SHALL appear only when all inventory facts are non-null. Completed and invalid-snapshot decisions SHALL have no inventory fields; a null-character decision SHALL also omit current level.

No inventory contents, account, credentials, authorization data, serialized character, timestamp, or random identifier SHALL be included. Logging/listener/exporter presence MUST NOT affect the returned decision or execution.

#### Scenario: Inventory-pressure explanation has exact facts once
- **WHEN** valid below-target inventory has free `9`
- **THEN** one decision event and activity contain matching status/reason/level/capacity/used/free/required values and no inventory contents

#### Scenario: Completed explanation omits inventory
- **WHEN** mining has reached target with otherwise valid inventory
- **THEN** one decision event and activity contain status/reason/current/target only and omit every inventory field

### Requirement: Scope remains narrow and fail-closed
The change MUST NOT add an inventory-remediation fallback, generic candidate/scoring framework, plan state machine, persistence, package, MockService behavior, credentials, production API access, live smoke, or main-host execution. It MAY change only the gathering repeat predicate necessary to enforce the selected target and ten-unit reserve against authoritative responses; other resource, movement, decomposition, craft, loot, and step semantics SHALL remain unchanged.

#### Scenario: Selector does not claim an unproven prerequisite
- **WHEN** mining remains below target but free inventory is below ten
- **THEN** the result is `Blocked`, not selected crafting, deletion, recycle, bank, or gathering
