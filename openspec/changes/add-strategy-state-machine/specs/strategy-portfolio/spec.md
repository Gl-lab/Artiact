## ADDED Requirements

### Requirement: Explained deterministic portfolio
The system SHALL rank feasible skill, combat and equipment candidates using configured useful value and full-cycle estimates, exposing scores and rejection reasons with ordinal tie-breaking.

#### Scenario: Competing goals and extensibility
- **WHEN** a fixed observation and policy contain mining, combat, equipment and woodcutting goals
- **THEN** the same candidates and winner are produced regardless of strategy registration order
- **AND** woodcutting uses the gathering implementation without a coordinator switch change
- **AND** skill, equipment and combat are simultaneously feasible with independently calculated full-cycle scores and policy variants selecting each category

### Requirement: Atomic guarded execution
The coordinator SHALL dispatch at most one mutating action per tick, after comparing a fresh observation fingerprint with the selected command's source fingerprint.

#### Scenario: Stale plan
- **WHEN** character, catalog or policy state changes before dispatch
- **THEN** the tick returns Replan and dispatches zero actions

#### Scenario: Completed command
- **WHEN** an action's returned state satisfies its postcondition
- **THEN** the command is consumed and the next tick plans from refreshed state
- **AND** a completed command is not replayed after a later failure

### Requirement: Bounded reconciliation
The coordinator SHALL retain attempts and authoritative returned state through cancellation or failure and SHALL reconcile unknown outcomes using read-only observation before permitting further mutation.

#### Scenario: Response lost after commit
- **WHEN** dispatch has unknown outcome and refreshed state satisfies the pending postcondition
- **THEN** a read-only tick reports Reconciled without repeating the POST
- **AND** the baseline-relative command transition is satisfied, attempts are not refunded and later mutation waits for observed cooldown expiration

#### Scenario: Ambiguous outcome or exhausted progress
- **WHEN** refreshed state cannot prove the postcondition or no-progress/decision limits are exhausted
- **THEN** mutation stops with a typed reason
- **AND** loss before commit, unrelated external changes and malformed refresh cannot clear ambiguity

#### Scenario: Cancellation, defeat and terminal goals
- **WHEN** cancellation arrives after a successful response, a fight loses or a postcondition fails
- **THEN** available authoritative state is retained and no following command executes
- **AND** all completed goals return Completed while unfinished wholly rejected goals return Blocked

### Requirement: Migration evidence
The system SHALL retain legacy mining and loot/craft entry points until replacement parity is proven and SHALL verify the new portfolio through deterministic real-client HTTP execution and replay.

#### Scenario: Offline acceptance
- **WHEN** the portfolio runs the supported synthetic profession/combat/equipment scenario twice
- **THEN** decisions, final state and ordered action trace match
- **AND** existing mining and bounded loot/craft suites continue to pass
- **AND** strategy-portfolio follows the literal 12-action, 13-decision, 69-second oracle in design.md, including separate equipment commands and both professions
