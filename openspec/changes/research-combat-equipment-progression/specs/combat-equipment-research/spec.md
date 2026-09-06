## ADDED Requirements

### Requirement: Traceable combat contract research
The research package SHALL map the required combat/equipment API subset and mechanics to dated official sources, local code and explicit compatibility or uncertainty status.

#### Scenario: Required route is evaluated
- **WHEN** fight, rest, equip, unequip or a required observation/movement route is assessed
- **THEN** the matrix records its method/path, access, request/response schemas, required fields, errors, cooldown and local compatibility evidence
- **AND** schema inspection is distinguished from live verification

#### Scenario: Evidence is absent or contradictory
- **WHEN** a required mechanic or payload cannot be established from available evidence
- **THEN** the package records the uncertainty and its effect on the proposed supported subset
- **AND** affected fixtures cannot be classified safe by assuming missing values

### Requirement: Comparative viability evidence
The research SHALL compare a local conservative predictor, optional official simulation and observed-outcome calibration, with independently justified safe, unsafe and boundary fixtures.

#### Scenario: Comparison is completed offline
- **WHEN** the alternatives are evaluated
- **THEN** their supported inputs, uncertainty, false-safe cases, reproducibility and maintenance costs are recorded against the same fixture set
- **AND** official simulation access is not required to run the experiments

#### Scenario: Viability cannot be established
- **WHEN** effects, randomness, stats or access exceed the model's evidenced subset
- **THEN** the model produces Unknown rather than a safe classification
- **AND** the experiment dispatches no fight

### Requirement: Isolated finite experiments
Executable prototypes SHALL be isolated from production and run offline with explicit positive decision, fight, recovery and no-progress limits, scripted authoritative responses and virtual cooldowns.

#### Scenario: A normal experiment executes
- **WHEN** a fixture is run
- **THEN** each decision dispatches at most one simulated mutating command and reserves its attempt before dispatch
- **AND** no credentials, network, main host or production retry path is used

#### Scenario: Terminal conditions are exercised
- **WHEN** fixtures exercise completion, unsafe or unknown combat, unreachable target, invalid state, inventory pressure, defeat, recovery failure, no progress or exhausted limits
- **THEN** each fixture terminates within its declared decision bound with independently asserted reason, state and command count
- **AND** recovery or equipment loops cannot reset the total budget

#### Scenario: Inputs are replayed
- **WHEN** a deterministic fixture is run twice with the same inputs
- **THEN** the ordered commands, decisions, terminal state and virtual cooldown total are identical

### Requirement: Authoritative state and unknown outcomes
The prototypes SHALL reconcile action response state before further decisions and stop mutation when an action outcome is unknown.

#### Scenario: Cancellation follows a successful response
- **WHEN** cancellation arrives after a scripted successful action response
- **THEN** its character state is retained and no following action is dispatched

#### Scenario: A response is lost
- **WHEN** the fake action port records a dispatch but returns an ambiguous failure
- **THEN** the prototype reports Blocked with an unknown-outcome reason and dispatch count of one
- **AND** neither a retry nor another mutating action occurs

#### Scenario: Defeat or changed state invalidates a plan
- **WHEN** returned HP, location, inventory or equipment contradicts the prediction
- **THEN** the returned state replaces the prediction and the next decision reevaluates or terminates according to the explicit recovery policy

### Requirement: Combat milestone and equipment semantics
The research SHALL define a positive combat-level milestone, terminal precedence, character-aware equipment prerequisites and the disposition of the existing LevelUpGoal.

#### Scenario: Milestone is already reached
- **WHEN** authoritative state satisfies the target
- **THEN** the prototype returns Completed without fight, rest or equipment mutation

#### Scenario: Candidate equipment is compared
- **WHEN** two items differ in level and usefulness against the supported opponent
- **THEN** the comparison considers current stats, loadout, slot/usage conditions and inventory constraints
- **AND** item level alone cannot establish an upgrade

### Requirement: Evidence-backed decision and handoff
The research SHALL deliver an ADR selecting a viability/recovery approach or a justified no-go, with reproducible evidence and a bounded Epic 6 handoff.

#### Scenario: Evidence supports a next implementation slice
- **WHEN** a go recommendation is issued
- **THEN** it identifies risk boundaries, supported mechanics, starting fixture and target, goal semantics, recovery and reconciliation rules, necessary DTO/client changes, minimum mock subset and Epic 6 acceptance cases
- **AND** it explains whether a full combat emulator is justified

#### Scenario: Critical uncertainty remains
- **WHEN** required safety or compatibility evidence is unresolved
- **THEN** the ADR records a no-go or explicitly blocked affected scope and a next bounded investigation
- **AND** proposal readiness or passing synthetic tests is not reported as production readiness

#### Scenario: Research is handed off
- **WHEN** research completion is reported
- **THEN** evidence lists exact verification commands/results, reviewed revision/diff, review type and unverified scope
- **AND** no production implementation, predecessor archival or real-character execution is implied
