# Artiact documentation

This directory is the source-grounded guide for developers and AI agents working on Artiact. The code remains authoritative when documentation and implementation disagree.

## Start here

| Document | Use it for |
|---|---|
| [Architecture](architecture.md) | Components, dependencies, startup, background execution, HTTP and observability |
| [Domain model](domain-model.md) | Goals, steps, crafting, inventory and looting-aware planning |
| [Mock service](mock-service.md) | Local game-API substitute, endpoints, data and gaps |
| [Development](development.md) | Setup, configuration, commands, tests and safe change workflow |
| [Development review, 2026-09-05](reviews/2026-09-05-development-process.md) | Evidence from September 4–5 commits and prioritized process improvements |
| [Official references](external-references.md) | Game site, wiki/concepts, OpenAPI, Swagger, changelog and operational API guides |
| [Known limitations](known-limitations.md) | Deliberate limits, incomplete paths and operational risks |
| [Combat/equipment research](research/combat-equipment/comparison.md) | Epic 5 evidence, isolated experiments and alternatives |
| [Combat viability ADR](decisions/0001-combat-viability-and-recovery.md) | Research decision and bounded Epic 6 handoff; live execution remains no-go |
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

Artiact starts a hosted worker automatically. It warms reference data, loads the configured character, repeatedly selects a goal, decomposes it into subgoals, builds executable steps and calls the game API. `GoalService.Evaluate` returns an immutable decision for `GoalSelection:MiningTargetLevel` (tracked default `20`). Below target, valid inventory with at least ten free units selects gathering. Reaching the target completes the milestone; malformed state or inventory pressure blocks progress. Completed/Blocked stops the worker normally. Mining resolves a deterministic eligible destination each cycle, performs at most one Move and one Gathering client invocation, and rechecks live target, inventory and XP. Scoped cycle/no-progress limits stop unproductive or exhausted runs. The socket-free `mining-progression` scenario proves target 3 in five decisions, six actions and 34 virtual seconds through real clients, with deterministic replay. This synthetic result does not prove live map access or guarantee target 20 with finite inventory.

The bounded looting-aware craft path can plan one missing non-craftable mob drop, acquire it through fights, and then consume it through a craft chain. See [Domain model](domain-model.md#looting-aware-crafting).

## Authority and maintenance

- Use code and tests as primary evidence for current behavior.
- Use this directory for explanations and constraints, not speculative roadmap items.
- Update the relevant document and local `AGENTS.md` whenever a change alters a public contract, runtime flow, command, configuration key or known limitation.
- Never place API credentials or character secrets in tracked Markdown or `appsettings*.json`.
