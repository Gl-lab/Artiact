## Why

`Artiact.MockService` currently supports fragments of character loading, movement, gathering, and crafting, but it cannot cold-start `GameClient.WarmUpCache()`, reset state deterministically, expose a stable action trace, or model cooldown passage without wall-clock waiting. Its repeated character GET overwrites accumulated state, fixture paths differ by process working directory, and dormant YARP/proxy code leaves an unnecessary production-network concept in a service that must remain local-only.

A minimal deterministic slice is required before explainable goal selection and mining progression can be evaluated safely without credentials or calls to the production Artifacts API.

## What Changes

- Add one named deterministic scenario, `basic-mining`, with a fixed initial character, fixed virtual-clock epoch, minimal maps/resources/items/monsters catalogs, and fixed move/gather transitions.
- Add mock-only reset, state, and ordered trace controls under `/__mock/*`.
- Make character loading initialize a scenario character once and retain later mutations until reset.
- Add paginated `GET /maps`, `/resources`, `/items`, and `/monsters` responses sufficient for cold-cache `GameClient.WarmUpCache()`.
- Make move and gathering transitions atomic, deterministic, and traceable.
- Advance a virtual clock immediately for configured action durations and return completed cooldown metadata without sleeping.
- Add an in-memory HTTP compatibility test that uses the real `GameHttpClient` and `GameClient`, an in-memory cache, and the mock host only.
- Remove YARP/proxy remnants and HTTPS redirection from the mock-only host so it has no production forwarding path.
- Keep all fixtures non-secret and prohibit `.env`, user-secret, production URL, and production-network access.

## Capabilities

### New Capabilities

- `deterministic-mock-scenario`: A local-only mock scenario can be reset, loaded through production client contracts, advanced through move and gather, and inspected as deterministic state and trace without wall-clock delay.

### Modified Capabilities

<!-- No archived MockService capability exists yet. -->

## Impact

- `Artiact.MockService/`: startup, scenario/state services, mock controls, catalog and action endpoints, deterministic fixtures, and removal of dormant proxy code/dependency.
- `Artiact.MockService.Tests/` or the smallest equivalent isolated test area: service invariants and in-memory HTTP compatibility coverage.
- `Artiact.sln`: include the deterministic mock tests in the default offline suite.
- `docs/mock-service.md` and `docs/development.md`: supported subset, commands, virtual-time divergence, and safety boundary.
- No production host startup, credential loading, real API request, database, broker, durable storage, combat, crafting expansion, or strategy-selection change.
