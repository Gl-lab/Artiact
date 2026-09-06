## ADDED Requirements

### Requirement: Finite response-driven combat progression
The combat run SHALL implement ADR 0001 with a positive target, configured positive decision/fight/rest/no-progress bounds, at most one command per decision, authoritative state reconciliation and sticky terminal outcomes.

#### Scenario: Baseline target completion
- **WHEN** the named synthetic baseline runs through real clients over TestServer
- **THEN** Move, Fight, Rest, Fight, Completed occur in five decisions and 29 virtual seconds
- **AND** final level is 2, XP is 0, HP is 14, free units are 8 and map identity is 2
- **AND** replay produces identical responses, decisions, state and trace

#### Scenario: Unsafe or unsupported observation
- **WHEN** stats, effects, access, target or inventory fail supported-subset validation
- **THEN** an explained Blocked decision issues no unsafe action

#### Scenario: Failure terminates progression
- **WHEN** defeat, rejected action, ambiguous outcome, invalid movement/equipment/recovery response, no progress or exhausted budget occurs
- **THEN** the run terminates within its bounds with an explicit reason and retains available authoritative state

#### Scenario: Cancellation after dispatch
- **WHEN** cancellation arrives during a successful in-flight action
- **THEN** returned state is saved before cancellation prevents cooldown waiting and any following command

### Requirement: Equipment and crafting prerequisites conserve state
The run SHALL compare equipment using character/opponent viability rather than item level, reconcile each separate swap action, and support one missing loot leaf with bounded fight/craft prerequisites.

#### Scenario: Pre-owned weapon improvement
- **WHEN** the gear fixture owns quick_blade and heavy_blade and wears old
- **THEN** it selects quick_blade, preserves old in returned inventory, and completes in seven decisions and 35 virtual seconds

#### Scenario: Shared craft dependency
- **WHEN** two recipe branches share a craftable dependency without a recursive path
- **THEN** planning succeeds when sufficient material exists, consumes each unit once and emits dependency crafts before consumers

#### Scenario: Missing mob ingredient
- **WHEN** one required leaf has supported reachable viable drop sources
- **THEN** ranking treats rate as reciprocal probability and execution acquires and consumes the actual returned ingredient before equipment use
