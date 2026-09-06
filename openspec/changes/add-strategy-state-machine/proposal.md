# Epic 7: explicit strategy portfolio

## Why
Mining and combat now have bounded response-driven vertical slices, but cannot compete in a common decision tick. The legacy tree can dispatch multiple actions and cannot reject a stale plan before dispatch.

## What Changes
- Add immutable observations with canonical fingerprints covering character, relevant catalogs and policy.
- Generate explained, deterministically scored candidates for skill milestones, combat milestones and owned equipment upgrades. Validate extensibility with woodcutting through the same gathering strategy as mining.
- Execute one atomic command after a fresh preflight observation; verify returned state, retain consumed attempts, and reconcile unknown outcomes read-only without blind replay.
- Register an explicit portfolio session, leaving existing mining and loot/craft entry points intact until their full semantic parity is demonstrated. Epic 8 provides staged inspect/one-shot host operation.

## Impact
Application services, DI, behavior tests, deterministic HTTP scenarios and documentation. No durable store, live action, automatic inventory deletion, bank or market. User's current instruction authorizes specification, development, commit and push for each epic.
