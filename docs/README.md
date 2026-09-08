# Artiact documentation

This directory is the source-grounded guide for developers and AI agents working on Artiact. The code remains authoritative when documentation and implementation disagree.

## Start here

| Document | Use it for |
|---|---|
| [Checkpoint diagnostics](checkpoint-diagnostics.md) | R16 sanitized failure phases, fail-closed file loading and unresolved historical transient |
| [Autonomous goals](autonomous-goals.md) | R11/R12 discovery, bounded dynamic milestones, budgets and lifecycle |
| [Autonomous recovery](autonomous-recovery.md) | R13 HP needs, explicit permissions, generated food and bounded preparation |
| [Combat goal discovery](combat-goal-discovery.md) | R14 next levels, catalog-derived opponents/gear and recovery prerequisites |
| [Autonomous quality evidence](autonomous-quality-2026-09-08.md) | R15 three-policy matrix, long-run outcomes, costs, stock and operational boundaries |
| [Autonomous live protocol](autonomous-live-protocol.md) | Separate unexecuted, explicitly scoped noncombat acceptance |
| [Full-path selection](full-path-selection.md) | R10 context-aware progress/cost estimates, factual run performance and comparison evidence |
| [Autonomous combat](autonomous-combat.md) | R9 stages, opponent/weapon/shield selection, preparation and immediate healing |
| [Architecture](architecture.md) | Components, dependencies, startup, background execution, HTTP and observability |
| [Domain model](domain-model.md) | Goals, steps, crafting, inventory and looting-aware planning |
| [Mock service](mock-service.md) | Local game-API substitute, endpoints, data and gaps |
| [Development](development.md) | Setup, configuration, commands, tests and safe change workflow |
| [Development review, 2026-09-05](reviews/2026-09-05-development-process.md) | Evidence from September 4–5 commits and prioritized process improvements |
| [Official references](external-references.md) | Game site, wiki/concepts, OpenAPI, Swagger, changelog and operational API guides |
| [Known limitations](known-limitations.md) | Deliberate limits, incomplete paths and operational risks |
| [Combat/equipment research](research/combat-equipment/comparison.md) | Epic 5 evidence, isolated experiments and alternatives |
| [Combat viability ADR](decisions/0001-combat-viability-and-recovery.md) | Research decision and bounded Epic 6 handoff; live execution remains no-go |
| [Staged operation](staged-operation.md) | Inspect/one-shot, auth, freshness, readiness and rollout boundary |
| [Bounded operation](bounded-operation.md) | Durable journal, bounded loop, exclusive local ownership and stop/status |
| [R6 lifecycle specification](../openspec/changes/run-lifecycle/proposal.md) | Offline result/archive commands, history and full-process recovery acceptance |
| [Local container rollout](container-rollout.md) | Mock-only Docker profile, durable volume, health/stop, Prometheus and OTLP-to-Zipkin acceptance |
| [Gathering with bank](gathering-bank.md) | Allowed deposits, retained stock, conserved bank/inventory and mock scenario |
| [Item production](item-production.md) | Independent quantity goals, nested resources, bank ingredients and atomic craft |
| [Skill preparation](skill-preparation.md) | Parent-bound weaponcrafting and mining prerequisites for item goals |
| [Capacity production](capacity-production.md) | Bounded batches, protected stock and orders larger than inventory |
| [Consumables](consumables.md) | Parent-bound healing, stock refill, cooking/fishing prerequisites and durable allowances |
| [Measured selection](measured-selection.md) | Alternative candidates, observed cooldown estimates and switching threshold |
| [Roadmap delivery](roadmap-progress.md) | Completed core epics, publication evidence and remaining live rollout boundary |
| [Управляемая автономность — следующий роадмап](roadmap-managed-autonomy-ru.md) | Согласованный план R16–R20: надёжность, панель, управление, реальная приёмка и регулярная работа |
| [Автоматический выбор целей — новый роадмап](roadmap-autonomous-goals-ru.md) | Проект R11–R15: генерация целей и их полезности без ручных Skill/Target/Value, динамические рубежи и приёмка |
| [Роадмап R6–R10 на русском](roadmap-next-ru.md) | Завершённый локальный объём R6–R10, включая R8a «Расходники и их производство»; история и критерии приёмки |
| [Предыдущий роадмап на русском](roadmap-ru.md) | История завершённых R1–R5 и ограниченной эксплуатационной приёмки |
| [Strategy portfolio](strategy-portfolio.md) | Explicit competing goals, atomic commands and reconciliation |
| [Deterministic combat progression](combat-progression.md) | Explicit bounded sessions, equipment, recovery and synthetic HTTP acceptance |

## Repository map

| Path | Responsibility |
|---|---|
| `Artiact/` | ASP.NET Core host, background automation, planning, executable steps, API client and JSON cache |
| `Artiact.Contracts/` | Shared API DTOs, goals, craft models and `IGameClient` boundary |
| `Artiact.MockService/` | In-memory/local-file substitute for a subset of the game API |
| `Artiact.Tests/` | xUnit/Moq unit and flow tests |
| `Artiact.MockService.Tests/` | Socket-free deterministic scenario and real-client compatibility tests |
| `Artiact.RealApiTests/` | Explicit offline checks and opt-in read-only smoke against the official API; excluded from `Artiact.sln` |
| `Artiact/cache/` | Repository JSON snapshots of maps, resources, items and monsters |
| `docker-compose.yml` | Local Prometheus, Grafana and Zipkin only; it does not run Artiact |

## Current product behavior

Artiact defaults to [staged inspect](staged-operation.md), which explains portfolio candidates without game actions. Explicit one-shot runs at most one action; legacy mode requires action opt-in. The retained legacy worker warms reference data, loads the configured character, repeatedly selects a goal, decomposes it into subgoals, builds executable steps and calls the game API. `GoalService.Evaluate` returns an immutable decision for `GoalSelection:MiningTargetLevel` (tracked default `20`). Below target, valid inventory with at least ten free units selects gathering. Reaching the target completes the milestone; malformed state or inventory pressure blocks progress. Completed/Blocked stops the worker normally. Mining resolves a deterministic eligible destination each cycle, performs at most one Move and one Gathering client invocation, and rechecks live target, inventory and XP. Scoped cycle/no-progress limits stop unproductive or exhausted runs. The socket-free `mining-progression` scenario proves target 3 in five decisions, six actions and 34 virtual seconds through real clients, with deterministic replay. This synthetic result does not prove live map access or guarantee target 20 with finite inventory.

The bounded looting-aware craft path can plan one missing non-craftable mob drop, acquire it through fights, and then consume it through a craft chain. See [Domain model](domain-model.md#looting-aware-crafting).

## Authority and maintenance

- Use code and tests as primary evidence for current behavior.
- Use this directory for explanations and constraints, not speculative roadmap items.
- Update the relevant document and local `AGENTS.md` whenever a change alters a public contract, runtime flow, command, configuration key or known limitation.
- Never place API credentials or character secrets in tracked Markdown or `appsettings*.json`.
