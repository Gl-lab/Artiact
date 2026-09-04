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

## Configuration

`Artiact/Program.cs` loads configuration from the build output directory in this order:

1. `appsettings.json`;
2. optional `appsettings.{environment-name-lowercase}.json`;
3. .NET user secrets.

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

On Bash/Git Bash, use `export ASPNETCORE_ENVIRONMENT=Dev` instead of `set`. The mock currently lacks several endpoints required by startup or looting; read [Mock service](mock-service.md) before treating this as an end-to-end check.

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

| Test class | Main responsibility |
|---|---|
| `CraftChainBuilderTests` | Recipe expansion and insufficient-resource behavior |
| `WearCraftTargetFinderTests` | Selecting craftable wearable targets from inventory |
| `TargetLootingResolverTests` | Mob subtype, level policy, drop rate and reachable map selection |
| `LootingCraftPlanningTests` | Missing mob leaves, nested recipes, resource accounting and fail-closed multiple leaves |
| `StepBuilderTests` | Craft order, workshop movement, live loot predicates and ten-fight bound |

There are 21 active facts. The suite is unit-focused; it has no controller, HTTP/authentication, hosted-service, configuration, telemetry, Docker or end-to-end coverage. Coverlet is installed as a collector, with no enforced threshold:

```text
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --collect:"XPlat Code Coverage"
```

## Change workflow

1. Read the nearest `AGENTS.md`, the relevant document in this directory and [Official references](external-references.md) for compatibility or game-mechanics work.
2. Confirm current behavior in code and tests; documentation is a guide, not a substitute for source inspection.
3. Write or update a failing test before behavior changes.
4. Keep changes within the owning project and update contracts/DI/callers together when signatures move.
5. Run focused tests, then the full solution test command.
6. Review `git diff` and `git status`; exclude secrets, cache refreshes, logs and `bin`/`obj` output.
7. Update documentation when runtime flow, commands, config keys, public contracts or limitations change.

## Repository hygiene

- `bin/`, `obj/`, logs, IDE state, `.env`, certificates and package artifacts are ignored.
- JSON reference-cache changes are data changes and should be reviewed separately from logic.
- The `.hermes/` planning directory is locally excluded through `.git/info/exclude` in this checkout.
- There is no discovered CI workflow in the repository; local verification is therefore the available gate unless one is added.
