## ADDED Requirements

### Requirement: Mutating requests have a single dispatch
The client SHALL send each action POST at most once per invocation and SHALL classify ambiguous outcomes without retrying. The autonomous worker SHALL terminate on an unknown action outcome rather than enter recovery repetition.

#### Scenario: Response is lost
- **WHEN** an action request encounters a network failure, timeout, server error or malformed successful response
- **THEN** the caller receives a typed unknown outcome and the client sends exactly one request
- **AND** the worker starts no subsequent cycle

#### Scenario: Request is rejected
- **WHEN** the API returns a non-success response below 500
- **THEN** the client returns a typed rejection containing the status code without the raw response body
- **AND** no retry occurs in that invocation

#### Scenario: Authentication fails
- **WHEN** token retrieval is unsuccessful or has no usable token
- **THEN** the action endpoint receives no request

### Requirement: Combat responses preserve controlled identity
The fight adapter SHALL select exactly one character whose name equals the configured character ordinally from data.characters and retain data.fight. Equipment SHALL use arrays with named slots and rest/equipment details SHALL remain available for postconditions.

#### Scenario: Wrong or duplicate participant
- **WHEN** a successful fight response lacks exactly one matching participant
- **THEN** it produces an unknown outcome and no alternate participant is substituted

#### Scenario: Named equipment request
- **WHEN** one weapon is equipped or unequipped
- **THEN** the request is a one-element array containing slot weapon and the action-specific fields
