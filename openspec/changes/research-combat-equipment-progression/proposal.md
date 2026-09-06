## Why

Bounded mining progression is implemented at `4b49b1e`, providing a tested incremental orchestration boundary. Combat progression cannot safely reuse the existing level-only loot eligibility heuristic or generic action response model. Epic 5 must establish an evidence-backed combat/equipment design before Epic 6 implementation.

## What Changes

- Research the current combat, defeat, recovery, equipment, effects, drops, movement and cooldown contracts, recording dated sources and local compatibility gaps.
- Compare a conservative local predictor, optional official simulation and observed-outcome calibration using isolated, disposable offline prototypes.
- Define a bounded single-character combat milestone, explicit decision reasons, recovery and unknown-outcome rules, and character-aware equipment prerequisites.
- Produce an ADR selecting a model or recording a justified no-go, plus the minimum API/mock subset and acceptance fixtures for Epic 6.

## Non-goals

No production combat progression, shared DTO migration, client retry fixes, new MockService endpoints, general state-machine rewrite, main-host execution, authenticated API calls or real fights. No bank, market, raids, multi-character strategy or full combat emulator. Preparing this proposal does not execute the research tasks or approve Epic 6.

## Capabilities

### New Capabilities

- `combat-equipment-research`: Reproducible research evidence, bounded offline experiments and a decision package for combat/equipment progression.

### Modified Capabilities

None. Existing runtime requirements remain unchanged.

## Impact

Planning artifacts live in this change. Research outputs will be authored under `docs/research/combat-equipment/` and an ADR under `docs/decisions/`; disposable executable experiments will live under this change's `experiments/`, outside the solution and host dependencies. Those directories are future deliverables, not completed work.

Research reads `Artiact`, `Artiact.Contracts`, both solution test projects and MockService. Any future production contract migration requires a separate implementation change covering callers, DI, compatibility tests and RealApiOffline checks. The existing mining implementation is the prerequisite; predecessor archival is separate bookkeeping and is not performed here.
