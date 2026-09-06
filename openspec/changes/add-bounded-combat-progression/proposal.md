## Why

Mining now has a deterministic bounded progression path. Combat research (ADR 0001) establishes a conservative supported subset, but the production fight/equipment transport cannot consume current responses, retries ambiguous actions, and has no bounded combat run. Epic 6 must prove the entire equipment, fight, recovery and loot/craft loop before strategy generalization.

## What Changes

- Migrate fight participant/result and named equipment array contracts with explicit compatibility adapters for existing steps; preserve rest and equipment details.
- Dispatch each action POST once. Distinguish rejected requests from unknown outcomes; suppress autonomous retries after ambiguity.
- Introduce a positive combat milestone, conservative normalized viability, map identity/access checks and finite one-command decisions with response-driven state and cooldowns.
- Add deterministic combat/equipment and loot/craft scenarios through real clients over TestServer, retaining mining behavior.
- Correct reciprocal loot ranking and shared craft dependency accounting with conservation tests.

## Capabilities

### New Capabilities

- `bounded-combat-progression`: Supported combat observations, bounded prerequisites, response reconciliation and deterministic acceptance.
- `single-dispatch-actions`: One HTTP dispatch per mutating invocation and explicit failure classification.

### Modified Capabilities

None. Existing mining decision semantics and mock fixtures remain supported.

## Impact

Contracts, application clients/services/DI, MockService, both test projects, RealApiOffline compatibility gates, and affected documentation. Implementation proceeds in buildable slices within this epic. Follow with Epic 7 specification only after Epic 6 acceptance.

## Non-goals

Live combat rollout, banking, market, multi-character scheduling, effects outside ADR 0001, durable plan storage, probabilistic risk policy, and autonomous recovery after defeat. These require separate evidence or decisions; completing the roadmap does not authorize real-character actions.
