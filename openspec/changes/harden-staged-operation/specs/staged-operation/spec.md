## ADDED Requirements

### Requirement: Operation-specific retry and authentication
The transport SHALL refresh expired/rejected tokens with bounded GET retries and SHALL never automatically replay an action POST.

#### Scenario: Expired read token
- **WHEN** a GET receives 401
- **THEN** at most one token refresh and one repeated GET occur
- **AND** a repeated rejection stops without leaking credentials

#### Scenario: Ambiguous or unauthorized action
- **WHEN** an action receives 401, 5xx or loses its reply
- **THEN** exactly one action POST was dispatched and its outcome is exposed without replay

### Requirement: Safe staged execution
The host SHALL default to inspect, require explicit action/live opt-ins, and limit one-shot to one mutating action plus optional read-only reconciliation.

#### Scenario: Inspect and one-shot
- **WHEN** the deterministic portfolio fixture is inspected and then explicitly run once
- **THEN** inspect issues zero game actions and one-shot issues at most one
- **AND** current candidate scores and typed completion/failure state are observable

### Requirement: Visible freshness and drift
The service SHALL expose independent liveness/readiness, reject stale observations and incompatible API subset/version, and isolate versioned cache entries from tracked snapshots.

#### Scenario: Stale or incompatible state
- **WHEN** API version/subset differs, observation collection exceeds freshness budget or the last successful observation expires
- **THEN** readiness is false with a typed reason and no staged action proceeds

#### Scenario: Corrupt cache
- **WHEN** a cache file is invalid, expired, future-dated or belongs to another endpoint/version
- **THEN** it is a cache miss without returning untrusted stale data

### Requirement: Offline release gates
The repository SHALL build without known compiler/advisory warnings and CI SHALL run solution and deterministic HTTP progression plus offline API boundary checks before staged rollout.

#### Scenario: Release evidence
- **WHEN** the epic is completed
- **THEN** exact offline commands, independent final review, publication revision and remaining live no-go are recorded
- **AND** no real-character run or optional strategic expansion is claimed
