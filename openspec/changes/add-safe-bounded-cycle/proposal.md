## Why

Artiact currently enters an autonomous worker loop whose `ActionService.Action()` performs five complete goal/decomposition/execution passes, making orchestration difficult to test and unsafe to point at a real account for verification. A local `.env` now provides real-server test credentials, so the project needs a bounded orchestration seam and an explicitly read-only, opt-in verification path before any real mutating strategy test is considered.

## What Changes

- Add an asynchronous orchestration operation that initializes once and executes exactly one top-level goal/decomposition/build/execution cycle per invocation.
- Make the hosted worker a thin caller of the bounded operation while preserving its repeated autonomous behavior and bounded recovery delay.
- Propagate cancellation through orchestration and waits touched by this slice so shutdown cannot start another cycle.
- Remove the hidden batch of five goal cycles from one `ActionService` invocation.
- Add an opt-in real-server smoke-test fixture that reads `.env` without executing it, authenticates, and performs only read operations required to verify credentials, character access, and reference-data compatibility.
- Keep real-server smoke tests excluded from the default test run unless an explicit opt-in variable is set.
- Ensure secret values are never printed, committed, included in test names, or attached to assertion failures.
- Do not execute move, gather, craft, fight, rest, equipment, inventory, bank, market, task, or other mutating real-server actions in this change.

## Capabilities

### New Capabilities

- `bounded-orchestration-cycle`: One cancellable orchestration invocation has explicit initialization and executes one top-level goal cycle without a hidden multi-cycle batch.
- `real-api-readonly-verification`: An explicit opt-in test path safely verifies real-server credentials and selected read contracts from an ignored `.env` without mutating game state or disclosing secrets.

### Modified Capabilities

<!-- No archived OpenSpec capabilities exist yet. -->

## Impact

- Main orchestration and worker code under `Artiact/Services/`, including `ActionService`, `IActionService`, and `ArtiactBackgroundService`.
- Cancellation signatures may affect step execution and `IGameClient` call sites only where required by the approved design; broad client-contract migration is excluded.
- New focused orchestration tests under `Artiact.Tests/Services/` and opt-in integration tests under a clearly separated real-API test area.
- Test configuration reads the repository-root `.env`, which remains ignored and untracked.
- No new production API endpoint, storage, service, broker, database, or background process is introduced.
