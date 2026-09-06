## MODIFIED Requirements

### Requirement: One invocation executes one top-level cycle
A cycle invocation SHALL read one initial current character snapshot and evaluate the pure target/inventory decision once. A terminal pure decision SHALL be the final decision. For a pure Selected decision, mining progression checks and resource resolution SHALL produce one final immutable decision before goal construction or mutation. The cycle SHALL explain and return that exact final decision. For final Selected, it SHALL construct the resolved execution goal, decompose it, build its graph and execute that graph once. Completed or Blocked SHALL return without constructing, decomposing, building or executing a goal. Read-only catalog I/O SHALL be permitted only after the pure decision and progression guards authorize resolution. No hidden loop SHALL start additional goal cycles.

#### Scenario: One successful cycle
- **WHEN** selection, progression guards and destination resolution authorize mining
- **THEN** a single resolved goal is constructed, decomposed, built and executed once; execution invokes game-client Move and Gathering at most once each, without bounding their internal HTTP retries or guaranteeing exactly-once server effects, and returns the final Selected decision

#### Scenario: One terminal cycle
- **WHEN** the pure decision is terminal or progression guards/resolution produce Blocked
- **THEN** the final terminal decision is returned with no execution graph or mutating actions

#### Scenario: Repeated worker operation
- **WHEN** the hosted worker receives Selected
- **THEN** it explicitly invokes the next cycle serially using the retained authoritative response state

#### Scenario: Terminal worker operation
- **WHEN** the hosted worker receives Completed or Blocked
- **THEN** it terminates normally without recovery delay or another cycle

### Requirement: Existing strategy semantics remain compatible
The change SHALL preserve non-mining decomposition order, crafting/resource-spending semantics, authoritative response updates and bounded looting behavior. Mining SHALL use deterministic resource/map resolution, live target/inventory/progress checks and at most one invocation of the game-client Gathering method per cycle. This replaces the mining repeat behavior and order-dependent resource selection; it SHALL NOT introduce a generic strategy state machine or autonomous inventory remediation.

#### Scenario: Existing gathering plan
- **WHEN** a final mining decision is Selected
- **THEN** the resolved destination is executed under the selected target and live inventory boundary, with further gathering delegated to the next explicit cycle

#### Scenario: Terminal gathering decision
- **WHEN** the final decision is Completed or Blocked
- **THEN** no gathering plan is constructed or executed

#### Scenario: Existing looting-aware crafting plan
- **WHEN** an independently supplied craft target requires one supported loot prerequisite
- **THEN** existing bounded fight-then-craft behavior remains available and its regression tests pass
