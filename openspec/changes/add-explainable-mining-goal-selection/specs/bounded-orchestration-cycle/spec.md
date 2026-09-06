## MODIFIED Requirements

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
