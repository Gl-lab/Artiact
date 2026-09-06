# Roadmap delivery — 2026-09-06

The September 4 roadmap's remaining core implementation epics were delivered in order: specification, implementation with offline acceptance and independent review, then commit/push before the next epic. Earlier epics 0–5 were already present at starting revision `05f7944`. This handoff does not claim real-character rollout or full legacy parity.

| Epic | Delivered scope | Publication / evidence |
|---|---|---|
| 6 | Bounded combat, target-aware owned equipment, recovery, conserved loot/craft chain, single-dispatch action contracts | Spec `4f5e736`; implementation through `3630f2d`; [evidence](../openspec/changes/add-bounded-combat-progression/execution-evidence.md) |
| 7 | Immutable observations, three competing goal categories, mining/woodcutting, deterministic scores, atomic commands, bounded reconciliation | Spec `662dda8`; implementation `9995813`; [evidence](../openspec/changes/add-strategy-state-machine/execution-evidence.md) |
| 8 | Default inspection, explicit one-shot/legacy opt-in, API subset/freshness guards, readiness, bounded auth/read retry, versioned cache, warning/dependency remediation and CI/container definition | Spec `db2addc`; [evidence](../openspec/changes/harden-staged-operation/execution-evidence.md) |

The legacy pipeline remains available explicitly because semantic parity is not claimed. The deterministic portfolio completes its literal 12-action, 69-second scenario and supports read-only reconciliation without replay. See [portfolio](strategy-portfolio.md), [combat](combat-progression.md), and [operation/configuration](staged-operation.md).

The offline implementation is complete. The final real-character stage of Epic 8 remains **not performed**, as the roadmap requires separate approval and [ADR 0001](decisions/0001-combat-viability-and-recovery.md) retains the live combat no-go. Docker execution, deployed health and telemetry delivery are also unverified locally; Docker is unavailable here. Optional bank/tasks/market/events/multi-character work has no approved objective and was not added.

Exact commands, test totals, reviewed diff and publication results belong in the dated Epic 8 evidence. User-owned `.serena/project.yml` and tracked reference cache snapshots were excluded from this work.
