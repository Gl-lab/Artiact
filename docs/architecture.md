# Architecture

## System context

Artiact is an ASP.NET Core application that hosts both operational HTTP endpoints and an autonomous `BackgroundService`. Its outward dependency is the Artifacts MMO-compatible HTTP API. For local development, `Artiact.MockService` implements only a subset of that API.

```mermaid
flowchart LR
    Host[ASP.NET Core host] --> Worker[ArtiactBackgroundService]
    Worker --> Action[ActionService]
    Action --> Goal[GoalService]
    Action --> Decomposer[GoalDecomposer]
    Action --> Builder[StepBuilder]
    Builder --> Steps[IStep graph]
    Steps --> Client[IGameClient / GameClient]
    Client --> HTTP[IGameHttpClient]
    HTTP --> API[Artifacts API or MockService]
    Client --> Cache[JSON reference-data cache]
    Host --> Health[/health]
    Host --> Metrics[/metrics]
    Host --> Telemetry[Console + Zipkin + Prometheus]
```

## Project boundaries

### `Artiact`

Executable host and application logic:

- `Program.cs` configures settings, logging, OpenTelemetry, DI, the hosted worker, `/health` and Prometheus scraping.
- `Services/` owns goal selection/decomposition, craft planning, map lookup, character state and action orchestration.
- `Resolvers/` contains the mob-drop resolver but uses the `Artiact.Services` namespace.
- `Models/Steps/` contains executable command objects.
- `Client/` owns authentication, API calls, retries, pagination and local reference-data caching.

### `Artiact.Contracts`

Shared boundary types used by the main app, tests and mock service: `IGameClient`, API DTOs, goals, `CraftTarget`, `CraftStep`, `LootPrerequisite` and map/resource value types. Changes here can affect every project.

### `Artiact.MockService`

Standalone ASP.NET Core service for deterministic local character, movement and gathering behavior for basic-mining and mining-progression. It is not a complete server emulator and is not currently a reverse proxy despite its `Artiact.SmartProxy` root namespace.

### `Artiact.Tests`

Unit and flow-oriented tests using xUnit and Moq. Tests focus on craft-chain construction, target selection, looting resolution and step execution.

## Startup and lifetime

`Program.Main` performs these steps:

1. Builds configuration from output-directory `appsettings.json`, optional environment-specific JSON and user secrets.
2. Registers logging, `HttpClient`, settings and OpenTelemetry.
3. Registers application services as scoped; `ActivitySource` is singleton. `AddGoalSelection` binds and validates the positive mining target on startup before worker initialization.
4. Registers `ArtiactBackgroundService`.
5. Maps `/metrics` and `/health`, then starts the web host.

`ArtiactBackgroundService.ExecuteAsync` creates one dependency-injection scope for its lifetime. It calls `IActionService.InitializeAsync(stoppingToken)` once, then calls `ExecuteCycleAsync(stoppingToken)` serially while decisions are Selected. Each call reads one planning snapshot, evaluates the pure selector and finalizes Selected through run guards and catalog resolution. It explains and returns the exact final immutable decision. Selected constructs a private ResolvedMiningGoal and executes one MiningStep; final Completed/Blocked performs no execution and terminates the worker normally without recovery delay. Mining invokes Move zero/one times and Gathering zero/one times; a later cycle reselects resources. AddMiningProgression binds validated limits and registers the scoped run state shared by ActionService/StepBuilder plus the production cooldown wait. A cycle failure is logged and followed by a cancellable 30-second recovery delay. Shutdown cancellation exits normally; other initialization failures are critical and terminate the hosted service.

> Running the main application is a side effect: the worker begins issuing game actions immediately after initialization.

## Action pipeline

```mermaid
sequenceDiagram
    participant B as BackgroundService
    participant A as ActionService
    participant G as GoalService
    participant D as GoalDecomposer
    participant S as StepBuilder
    participant C as IGameClient

    B->>A: InitializeAsync(stoppingToken)
    A->>C: WarmUpCache()
    A->>C: GetCharacter()
    loop while Selected
        B->>A: ExecuteCycleAsync(stoppingToken)
        A->>G: Evaluate(snapshot)
        G-->>A: Preliminary GoalDecision
        A->>A: Progression guards and reserve attempt
        A->>C: Resolve catalog destination if allowed
        A->>A: Explain final decision
        alt Selected
            A->>D: DecomposeGoal(new ResolvedMiningGoal(target, destination))
            A->>S: BuildStep(private goal)
            S-->>A: IStep graph
            A->>C: Move zero/once, gather zero/once with live checks
        else Completed or Blocked
            Note over A,C: No decomposition, building or actions
        end
        A-->>B: Exact final decision
        Note over B: Stop normally on Completed/Blocked
    end
```

The selector fails closed before decomposition when below-target inventory is invalid or has fewer than ten free units. Existing independently supplied goal decomposition/crafting remains available; it is not an autonomous inventory-remediation fallback. Before actions and after movement, MiningStep checks target, inventory, valid XP and resource eligibility. Changed level or wrong movement destination suppresses gathering. Responses are saved and gather progress is accounted before cancellation; the next cycle reports terminal effects.

## Step execution model

- `MixedStep` executes child steps sequentially.
- `ConditionalStep` evaluates live character state and may skip its child.
- `MoveStep` moves to a map point for generic/craft paths.
- MiningStep owns one resolved destination and bounded move/gather execution, using the injected IMiningCooldownDelay for returned total seconds.
- `GatheringStep` and generic `ActionStep` call `IGameClient` actions.
- `ActionStep` saves the returned character and delays for the API cooldown. It can repeat while a predicate remains true and may enforce a maximum attempt count.

Subgoals are built before their parent goal, so prerequisite craft or inventory work executes first.

## API client and cache

`GameHttpClient` obtains a token from `/token` using Basic authentication, then uses the returned Bearer token. `GameClient` exposes character actions and paginated map/resource/item/monster reads.

Action calls dispatch once. Network/timeout/read/JSON failures and HTTP 5xx produce sanitized `ActionFailureException(UnknownOutcome)`; other unsuccessful responses produce `Rejected` with the status code. Both stop the worker without recovery repetition. Token rejection prevents action dispatch. Calls remain tokenless and expired Bearer tokens are not refreshed after a 401. Fight adapts exactly one ordinal-name participant from `data.characters` into `Data.Character` and retains `data.fight`; named equipment requests use one-element arrays. Full combat normalization and progression are not yet implemented.

`CacheService` stores one JSON file per reference-data element type under a relative `cache` directory and treats it as fresh for 48 hours. The cache path therefore depends on the process working directory.

## Observability

- Console and NLog logging.
- `ActionService` alone emits one Information `GoalDecision` event per evaluated cycle and matching activity tags: `goal.decision.status`, `goal.decision.reason`, `goal.mining.target_level`; observed current level adds `goal.mining.current_level`. Valid inventory facts add `goal.inventory.capacity`, `.used`, `.free`, `.required_free` (each with the `goal.inventory` prefix). Completed/invalid snapshots omit inventory fields; absent characters omit current level. No character/account/inventory contents enter the decision event. Worker logs do not duplicate it; tracing listeners are optional.
- Final Selected adds resource code/level and destination X/Y. Selected and progression-only Blocked add attempted_cycles, max_cycles, consecutive_no_progress and max_no_progress under goal.mining. Other terminal decisions omit these fields. Failed catalog loading emits no fabricated decision; it retains the attempt and propagates through existing error recovery.
- W3C activity IDs and `ActivitySource("Artiact.Client")`.
- ASP.NET Core and `HttpClient` tracing.
- Console and Zipkin trace exporters.
- `Meter("Artiact.Application")` and Prometheus exporter.
- `/health` returns a static healthy response and UTC timestamp; it does not probe the game API, cache, worker state or Zipkin.

`docker-compose.yml` starts Prometheus on 9090, Grafana on 3000 and Zipkin on 9411. Artiact itself runs outside Compose. The committed Prometheus target `localhost:5000` resolves inside the Prometheus container and does not reach a host-run Artiact process as configured.
