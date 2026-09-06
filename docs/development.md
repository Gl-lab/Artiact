# Development guide

## Prerequisites

- .NET SDK 9. `global.json` requests `9.0.100`, rolls forward to the latest installed minor release and disallows prerelease SDKs.
- Docker Compose only if local Prometheus, Grafana and Zipkin are needed.
- Network/API credentials only for running against the real Artifacts API.

## Restore, build and test

Run from the repository root:

```text
dotnet restore Artiact.sln
dotnet build Artiact.sln --no-restore
dotnet test Artiact.sln --no-restore
```

Focused looting/crafting checks:

```text
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~TargetLootingResolverTests
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~LootingCraftPlanningTests
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~StepBuilderTests
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~WearCraftTargetFinderTests
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~CraftChainBuilderTests
```

Deterministic MockService checks (socket-free `TestServer`; no credentials or production API):

```text
dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore
dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~GameClientCompatibilityTests
```

## Isolated combat research experiments

Epic 5's disposable test project is excluded from `Artiact.sln` and all host dependencies. It references Contracts only for offline payload probes; no production client, credentials, network calls or real cooldown waits are used during test execution. Restore may access NuGet.

```text
dotnet restore openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj
dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore
```

Every deterministic fixture contains replay checks. Run this separate command when changing the research prototype; the solution gate does not discover it. See [protocol](research/combat-equipment/experiments.md) and [ADR](decisions/0001-combat-viability-and-recovery.md) for its supported subset and limits. This project is not an emulator or a production combat feature.

## Configuration

`Artiact/Program.cs` loads configuration from the build output directory in this order:

1. `appsettings.json`;
2. optional `appsettings.{environment-name-lowercase}.json`;
3. .NET user secrets.

`GoalSelection:MiningTargetLevel` is the only goal-selection option; tracked `appsettings.json` sets `20`. `Program` and safe DI tests call the same `AddGoalSelection(IServiceCollection, IConfiguration)` extension. Missing, non-integer, zero or negative targets fail binding/validation during startup before autonomous initialization. No fallback target or configurable inventory threshold exists. Direct selector evaluation of a non-positive target returns typed Blocked instead.

Focused offline decision checks (no host, credentials or network):

```text
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~GoalServiceTests|FullyQualifiedName~GoalDecisionFactoryTests"
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~MiningBoundaryTests
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~DecisionObservabilityTests
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~GoalSelectionConfigurationTests
```

Completed/Blocked terminates autonomous repetition normally without recovery delay. Inspect the single ActionService `GoalDecision` event for typed reason/facts. Inventory-pressure remediation and periodic refresh are deferred; restart reloads server state and reevaluates. A still-running web host does not imply a still-running worker.

`ApiSettings` requires:

- `BaseUrl`;
- `Username`;
- `Password`;
- `Character`.

`ZipkinSettings` requires `Endpoint`.

Tracked configuration intentionally contains only non-secret endpoints. Store credentials and character-specific values in user secrets or environment variables. Example commands:

```text
dotnet user-secrets --project Artiact/Artiact.csproj set "ApiSettings:Username" "<username>"
dotnet user-secrets --project Artiact/Artiact.csproj set "ApiSettings:Password" "<password>"
dotnet user-secrets --project Artiact/Artiact.csproj set "ApiSettings:Character" "<character>"
```

Do not commit the substituted values.

### Isolated read-only real API verification

`Artiact.RealApiTests` is intentionally excluded from `Artiact.sln`. The default build and test commands never load the repository-root `.env` and never contact the real API.

Run its parser, destination, redirect, allowlist and sanitization checks offline:

```text
dotnet restore Artiact.RealApiTests/Artiact.RealApiTests.csproj
dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline
```

The separate restore is necessary on a fresh checkout because solution restore excludes this project. Run these offline checks whenever its code or the shared DTOs it consumes change; do not run the project without a category filter.

The live smoke is a separate explicit command. In Git Bash:

```text
ARTIACT_REAL_API_READONLY=1 dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --filter Category=RealApiLive
```

The live command reads the ignored root `.env` only after the exact opt-in guard passes. It accepts the `ApiSettings__*` keys or their documented `API_*` aliases, pins credentials to `https://api.artifactsmmo.com` with redirects disabled, performs only `POST /token` followed by GET requests for the character and one page each of maps, resources, items and monsters, and emits only status/count evidence. Any `/action/` request is prohibited. Running the normal `Artiact` host is not a read-only smoke test because its worker performs game actions.

## Running

### Against the real API

```text
dotnet run --project Artiact/Artiact.csproj
```

This starts the background worker and may immediately perform real game actions. Do not use it as a harmless compilation check.

### Against MockService

Use two terminals:

```text
dotnet run --project Artiact.MockService/Artiact.MockService.csproj --launch-profile http
```

```text
set ASPNETCORE_ENVIRONMENT=Dev
dotnet run --project Artiact/Artiact.csproj
```

On Bash/Git Bash, use `export ASPNETCORE_ENVIRONMENT=Dev` instead of `set`. The mock implements the deterministic `basic-mining` and `mining-progression` slices. The autonomous worker can still select an unsupported goal/action, so use the TestServer compatibility suite rather than starting the main host as a general smoke test. See [Mock service](mock-service.md).

### Monitoring

```text
docker compose up -d
docker compose down
```

Default local endpoints:

- Artiact health: host-selected URL plus `/health`;
- Artiact metrics: `/metrics`;
- Prometheus: `http://localhost:9090`;
- Grafana: `http://localhost:3000`;
- Zipkin: `http://localhost:9411`.

The Compose file contains development-only Grafana credentials (`admin`/`admin`) and uses mutable `latest` image tags. Do not reuse this configuration for production. Its Prometheus target is `localhost:5000` inside the Prometheus container, so it cannot scrape a host-run Artiact instance without a host-gateway or network target change.

The current `Artiact/Dockerfile` does not have a working obvious build context for the multi-project solution: repository-root context lacks the project file expected by `COPY *.csproj`, while project-directory context excludes `Artiact.Contracts`. Treat container-image build as unresolved until the Dockerfile is corrected.

## Test map

`SingleDispatchTests` covers action POST failures, response loss, token rejection and terminal worker behavior. `CombatContractTests` covers controlled fight identity and equipment/rest wire details. Run both with `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~SingleDispatchTests|FullyQualifiedName~CombatContractTests"`. These authored fragment tests do not establish complete combat compatibility.

| Test class | Main responsibility |
|---|---|
| `CraftChainBuilderTests` | Recipe expansion and insufficient-resource behavior |
| `WearCraftTargetFinderTests` | Selecting craftable wearable targets from inventory |
| `TargetLootingResolverTests` | Mob subtype, level policy, drop rate and reachable map selection |
| `LootingCraftPlanningTests` | Missing mob leaves, nested recipes, resource accounting and fail-closed multiple leaves |
| `StepBuilderTests` | Craft order, workshop movement, live loot predicates and ten-fight bound |
| `ActionServiceTests` | Exact decision identity, terminal isolation, fresh execution graphs, cancellation and optional tracing |
| `GoalServiceTests` / `GoalDecisionFactoryTests` | Deterministic precedence, malformed snapshots and immutable construction invariants |
| `MiningDestinationResolverTests` / `MiningGoalDecisionFactoryTests` | Progression resolver ranking/catalog validation and progression-only reason invariants |
| MiningRunTests / MiningProgressionTests | Guard precedence, counters, reset/failure semantics and exact progression telemetry |
| MiningStepTests / MiningExecutionBoundaryTests | One-gather execution, live guards, resolved goals and controlled cooldown cancellation |
| MiningProgressionConfigurationTests | Production limit binding/validation and isolated scopes |
| MiningProgressionScenarioTests / MiningProgressionFlowTests | Synthetic scenario literals, atomic/replayed transitions and full-client five-cycle worker/manual acceptance |
| MiningBoundaryTests | Live move/gather responses, target/reserve boundaries and two-cycle terminal worker flows |
| `DecisionObservabilityTests` | Exact structured event/activity fields, omission, cardinality and listener equivalence |
| `GoalSelectionConfigurationTests` | Production registration and startup validation without hosting |
| `GoalDecomposerTests` | Gathering decomposition without a trace listener |
| `ArtiactBackgroundServiceTests` | Worker repetition, recovery delay and normal cancellation |
| `StepCancellationTests` | Pre-action cancellation, cooldown cancellation and authoritative-state reconciliation |
| `Artiact.MockService.Tests` | Deterministic reset/catalog/character/move/gather behavior, replay, concurrency, route allowlist and real-client TestServer compatibility |

The solution runs application tests and socket-free MockService tests. The separate `Artiact.RealApiTests` project contains offline configuration/HTTP-boundary checks and an explicit live smoke, but is not part of the solution. Record exact totals with the tested revision in change evidence; do not use a static test count as a completion gate. The default solution performs no production network access. Coverlet is installed as a collector, with no enforced threshold:

```text
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --collect:"XPlat Code Coverage"
```

## Change workflow

1. Read the nearest `AGENTS.md`, the relevant document in this directory and [Official references](external-references.md) for compatibility or game-mechanics work.
2. Confirm current behavior in code and tests; documentation is a guide, not a substitute for source inspection.
3. Define observable acceptance criteria and relevant failure cases before implementation. Run the new/changed behavior test before production edits; confirm that RED exposes the intended defect. If it is already GREEN, record that and reassess the change instead of manufacturing a failure.
4. Keep changes within the owning project and update contracts/DI/callers together when signatures move.
5. Run focused tests, then the full solution test command.
6. Review `git diff` and `git status`; exclude secrets, cache refreshes, logs and `bin`/`obj` output.
7. Update documentation when runtime flow, commands, config keys, public contracts or limitations change.

### Scope and planning

For a small fix, a short problem/acceptance/test note in the task or PR is enough. For a new capability, cross-project contract change, or risky execution change, use the existing `openspec/changes/<change>/` structure: proposal for scope and non-goals, design for decisions, specs for observable behavior, tasks for implementation and verification. Read the relevant artifacts before editing. Keep permanent behavior in `docs/`; avoid copying the entire specification into instructions.

Choose independently testable slices, with code, tests and affected documentation together. Separate unrelated capabilities (for example, orchestration and a real-API verifier) when they can be delivered independently. Keep shared signature migrations atomic across callers. WIP checkpoints must be clearly marked and must not be treated as verified completion. Keep cache refreshes and local tooling configuration separate.

### Review and completion evidence

Review acceptance criteria, changed contracts and relevant failure paths before style. For craft planning, consider shared/nested ingredients and execution order; for actions, consider cancellation and authoritative response state; for mock changes, consider replay, non-mutation on rejection and real-client compatibility. Use independent expected values for contract tests, and avoid deriving the expected result from the same fixture or algorithm under test.

Classify findings as reproducible blockers, missing acceptance coverage, or optional improvements. A blocker needs a concrete trigger and expected/actual behavior; test a reviewer's prediction before changing production code. Rerun affected checks after fixes and the solution gate on the final code. Reopen settled review only for a new defect, changed contract, or invalidated evidence; track unrelated improvements separately. An independent review, when requested, must identify the reviewed diff and must not be claimed when only self-review occurred.

Record a compact result in the PR, final handoff, or an existing change's verification section:

```text
Scope and acceptance criteria: ...
Tested base/revision and local diff: ...
Commands and results: <exact command>, <passed/failed/skipped>, <relevant warnings>
Review: <self/independent>, <reviewed revision or diff identity>, <blockers resolved/open>
Not verified: <live/API/other scope and reason>
Documentation and remaining tasks: ...
```

Do not equate `[verified]`, checked task boxes or a GREEN unit suite with live compatibility. Keep offline completion and external verification status distinct. Close or archive an OpenSpec change only when its required tasks are actually resolved; explain deferred work explicitly. Historical publication tasks are separate from implementation evidence. Use actual newlines in commit bodies.

When behavior changes, search related prose, diagrams, root/nested `AGENTS.md` and limitations for old method names, deleted fixtures and removed configuration. Fix impacted statements in the same slice; add a new instruction only when it prevents a concrete recurring mistake.

## Repository hygiene

- `bin/`, `obj/`, logs, IDE state, `.env`, certificates and package artifacts are ignored.
- JSON reference-cache changes are data changes and should be reviewed separately from logic.
- The `.hermes/` planning directory is locally excluded through `.git/info/exclude` in this checkout.
- GitHub Actions workflow `.github/workflows/ci.yml` runs the solution build/tests and the separately restored `Category=RealApiOffline` boundary suite as independent credential-free jobs on pushes and pull requests to `master`. It does not start the main host or opt into live API tests.

## Bounded mining progression

Program calls AddMiningProgression alongside AddGoalSelection. Required positive integer settings are `MiningProgression:MaxCycles` (tracked 100) and `MiningProgression:MaxConsecutiveNoProgress` (tracked 3); no-progress must not exceed cycles. Missing, malformed, zero, negative or inverted values fail startup validation without starting the worker. Limits are captured once per scope. Successful explicit initialization resets the run; cycle failure and cancelled/failed reinitialization do not refund attempts.

Mining produces one final decision per normal cycle, with at most one Move and one Gathering method invocation. Completed/Blocked stops normally without recovery delay; loading/action exceptions propagate and retain reserved budget. Returned cooldown totals remain honored in production. Tests inject recorded instant/controlled waits.

Focused offline checks:

```text
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~Mining|FullyQualifiedName~GoalDecision|FullyQualifiedName~ActionService|FullyQualifiedName~DecisionObservability|FullyQualifiedName~StepCancellation|FullyQualifiedName~StepBuilder|FullyQualifiedName~LootingCraftPlanning|FullyQualifiedName~ArtiactBackgroundService"
dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore
```

MiningProgressionFlowTests uses real application services and clients over TestServer with in-memory cache, sentinel credentials and no real cooldown sleep. Both manual and worker drivers reproduce four Selected decisions then Completed at level/XP 3/4, six actions and 34 virtual seconds, including full independent response oracles and reset/replay. Boundary injection covers wrong movement, unchanged XP, invalid state, exhausted inventory, low budget and cancellation without adding mock HTTP control endpoints. Live API and main-host execution remain outside this verification.
