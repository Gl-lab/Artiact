## Purpose

Complete a reproducible mining milestone using current character responses, deterministic catalog selection and finite action-cycle bounds.

## ADDED Requirements

Action counts and live preconditions in this capability SHALL apply at the application-to-game-client method boundary: at most one invocation of `Move` and one invocation of `Gathering` per selected cycle. They SHALL NOT imply a bound on HTTP attempts or exactly-once server effects. Existing client retries can issue multiple POSTs inside one invocation without reentering application guards; changing that retry behavior and reconciling unknown outcomes remain outside this change. Exact mock trace/action counts below apply to the declared successful, retry-free scenario.

### Requirement: Mining destinations are selected deterministically from the supported catalog subset
For a below-target character with valid inventory and progress facts, the system SHALL join resources with maps by ordinal code and map content type `resource`. Eligible resources SHALL have skill `mining` under ordinal-ignore-case matching, nonblank code, level at least 1 and at most `max(1, mining level)`, and mining level less than resource level plus 10. The last comparison SHALL use wide arithmetic. A resource without a matching map SHALL be skipped. Pairs SHALL rank by resource level descending, Manhattan distance ascending using wide arithmetic, resource code ordinal ascending, X ascending, then Y ascending. Input ordering SHALL NOT affect selection.

This capability SHALL define a destination as a matching coordinate in the existing single-plane catalog contract; it SHALL NOT claim verification of real-world layers, access conditions, transitions or optimal travel time. For catalog values actually returned to the resolver, null lists/elements, blank resource codes, resource levels below 1, duplicate resource codes, and duplicate map coordinates SHALL yield `Blocked/invalid_mining_catalog`. Null map content or blank map content code SHALL mean no resource on that tile. A valid empty catalog or no eligible joined pair SHALL yield `Blocked/no_mining_destination`. Valid non-mining resources SHALL be ignored.

Failures thrown by the existing client before it returns catalog values, including HTTP, deserialization and missing required payload data errors, SHALL remain loading failures rather than `invalid_mining_catalog`. Initialization failure SHALL propagate and start no decision cycles. A catalog-loading exception during a selected cycle SHALL propagate through existing worker recovery, retain the reserved cycle attempt and emit no fabricated final decision. The resolver SHALL NOT classify errors by exception message or convert arbitrary client exceptions into a terminal catalog decision. This change SHALL NOT modify client payload parsing or retry behavior.

#### Scenario: Null resource data fails cold initialization
- **WHEN** the real client receives a successful `/resources` response with `data: null` during cold cache warm-up
- **THEN** the existing loading exception propagates, no cycle or mutating action starts, and no `Blocked/invalid_mining_catalog` decision is fabricated

#### Scenario: Returned invalid values and thrown loading errors are distinct
- **WHEN** the resolver receives a null list from its catalog provider
- **THEN** it returns `Blocked/invalid_mining_catalog` before goal execution
- **WHEN** that provider instead throws before returning catalog values during a selected cycle
- **THEN** the exception propagates, the reserved attempt remains consumed, no mutating action starts, and no final decision event is fabricated

#### Scenario: Unmapped highest resource does not hide a feasible lower resource
- **WHEN** level 2 has an unmapped level-2 resource and a mapped level-1 mining resource
- **THEN** the mapped level-1 resource is selected without throwing

#### Scenario: Stable ties and useful resources
- **WHEN** equal-level resources have several maps and the catalog order is permuted
- **THEN** ranking chooses the same pair; a resource ten levels below the character is never selected

#### Scenario: No destination and malformed catalog are distinct
- **WHEN** the catalog is empty or has only locked/non-mining/unmapped resources
- **THEN** the final decision is `Blocked/no_mining_destination` with no move or gather
- **WHEN** the catalog instead has a duplicate code or coordinate, null element or invalid required resource field
- **THEN** the final decision is `Blocked/invalid_mining_catalog` with no move or gather

### Requirement: Each selected mining cycle invokes gathering at most once
A selected mining cycle SHALL carry its resolved resource code, level and coordinates into execution without independently selecting another destination. It SHALL recheck target, inventory, progress validity and resource eligibility before movement and again after any movement, verify actual coordinates before gathering, invoke Move and Gathering at most once each, and save each authoritative response before cancellation or cooldown handling. A changed mining level after movement SHALL suppress gathering and allow reselection on the next cycle. At the destination no redundant move SHALL occur. Every normal selected cycle SHALL return Selected; effects that require termination SHALL be reported by the next decision cycle.

#### Scenario: Level-up causes resource reselection
- **WHEN** a gather raises level 1 to level 2 and a level-2 resource exists at another coordinate
- **THEN** no repeated gather occurs in that cycle and the next cycle selects the level-2 resource and moves before gathering

#### Scenario: Movement does not authorize gathering elsewhere
- **WHEN** a successful move response reports coordinates other than the selected destination
- **THEN** that response is retained, no gather occurs, and the next otherwise-eligible cycle returns `Blocked/mining_destination_not_reached`

#### Scenario: Cancellation reconciles the successful response
- **WHEN** cancellation arrives while move or gather is in flight and the call returns successfully
- **THEN** the returned character is saved, cancellation propagates, and no subsequent wait or action begins

### Requirement: Progression has explicit run-local termination bounds
`MiningProgression:MaxCycles` and `MiningProgression:MaxConsecutiveNoProgress` SHALL be required positive integers, with the latter at most the former. Tracked values SHALL be 100 and 3 respectively; absent, malformed, zero, negative or inverted values SHALL fail startup validation before initialization. Successful explicit initialization SHALL reset counters; ordinary cycles SHALL NOT reset them. Separate service scopes SHALL NOT share counters.

The existing pure target/inventory decision SHALL take precedence. If it is Selected, the final decision SHALL next check invalid progress facts, pending movement postcondition failure, no-progress threshold, cycle budget, then catalog resolution, in that order. Valid below-target progress SHALL require nonnegative mining XP, positive mining max XP and XP less than max XP. Invalid values SHALL yield `Blocked/invalid_mining_progress`. At least one of a higher mining level or higher XP at the same level after a gather SHALL count as progress; level-up with residual XP reset SHALL count. Non-increase SHALL increment the consecutive counter; progress SHALL reset it. Movement alone SHALL NOT count as mining progress or as a failed gather. After N consecutive unsuccessful-progress gather responses the next otherwise-eligible cycle SHALL return `Blocked/mining_no_progress` without mutation.

After the pure Selected decision and progression guards pass, the cycle budget SHALL reserve one attempt before catalog I/O or execution; failed attempts SHALL retain their consumed budget. Once MaxCycles have been consumed, the next otherwise-eligible cycle SHALL return `Blocked/mining_cycle_limit` without further catalog I/O or actions. Terminal evaluations SHALL require no extra budget. Infrastructure exceptions SHALL retain existing propagation/recovery behavior; this bound SHALL NOT be described as an HTTP timeout, unknown-outcome reconciliation or permission to retry mutations.

#### Scenario: No-progress termination
- **WHEN** the limit is 3 and three gathers return unchanged mining level and XP despite item gains
- **THEN** there are three gathers followed by one `Blocked/mining_no_progress` cycle with no fourth gather and no terminal recovery delay

#### Scenario: Completion takes precedence over budget and inventory
- **WHEN** the last permitted gather reaches target and leaves fewer than ten free units
- **THEN** the next cycle returns Completed, with no inventory facts or additional action

#### Scenario: Failed attempts cannot replenish the cycle budget
- **WHEN** MaxCycles is 2 and two otherwise-selected cycles throw during catalog retrieval
- **THEN** both attempts propagate normally and consume budget; the third cycle blocks on the budget without another retrieval

#### Scenario: Level-up residual XP is progress
- **WHEN** a gather changes level/XP from 1/6 to 2/2
- **THEN** the consecutive no-progress count resets to zero

### Requirement: Inventory pressure remains an explicit terminal boundary
The system SHALL preserve the existing shared inventory validation and ten-unit start reserve. It SHALL NOT choose crafting, deletion, recycling, banking or any other inventory remediation. It SHALL document that a target requiring more inventory than available is not guaranteed to complete.

#### Scenario: Exact reserve boundary
- **WHEN** a gather starts with free 10 and returns free 9 below target
- **THEN** only that gather occurs and the next cycle is `Blocked/inventory_pressure`

### Requirement: Progression is verified through bounded offline orchestration
A socket-free full-client run of `mining-progression` with target 3, MaxCycles 10 and no-progress limit 3 SHALL produce exactly four Selected cycles followed by Completed: move to (2,0), two gathers, move to (4,0), two gathers. Final mining level/XP/max XP SHALL be 3/4/10, copper ore quantity 2 and iron ore quantity 2, with virtual elapsed time 34 seconds and six trace entries. A second reset and fresh initialized run SHALL reproduce every state and ordered trace field except generation, normalized explicitly. Application waits SHALL be recorded and completed by the test harness without wall-clock sleeping; production waits SHALL continue honoring returned total cooldown duration and cancellation.

#### Scenario: Complete autonomous milestone
- **WHEN** real orchestration and clients run the named scenario through the isolated test HTTP handler
- **THEN** all exact cycle, state, trace and terminal assertions above pass without main-host startup, credentials, disk cache, sockets or production requests

#### Scenario: Low budget is an expected outcome
- **WHEN** the same scenario uses MaxCycles 1 and no-progress limit 1
- **THEN** one move and one gather occur, followed by `Blocked/mining_cycle_limit` at level 1 and XP 6
