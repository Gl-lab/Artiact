# Staged operation and release boundary

The host defaults to `Execution:Mode=Inspect`. It validates configuration, probes the supported OpenAPI subset, loads a fresh observation and logs candidate scores/rejections without invoking game actions. With missing credentials or portfolio settings it reports configuration failure and stays unready. Starting a process is still not a compilation check: explicitly configured modes can act.

## Configuration and modes

Configuration order is output-directory `appsettings.json`, environment JSON, user secrets, environment variables, then command-line arguments. Later sources win. Environment-specific files use the lowercase environment name. Credentials remain in environment variables/user secrets; `.env` is not loaded by the host.

| Setting | Behavior |
|---|---|
| `Execution:Mode` | `Inspect` default; explicit `OneShot` or `Legacy` |
| `Execution:AllowActions` | Default false; required for OneShot/Legacy |
| `Execution:LiveActionsApproved` | Default false; additionally required for non-loopback actions |
| `Execution:ExpectedApiVersion` | Supported version, currently `8.2.3`; mismatch blocks staged execution |
| `Execution:FreshnessSeconds` | 1–300, default 30; bounds observation collection and readiness age |
| `Portfolio:Skills` | Array of `{Skill, Target, Value}`; mining/woodcutting use one strategy |
| `Portfolio:CombatTarget`, `Monster`, `Equipment` | Explicit milestone, supported opponent and owned weapon goal |
| `Portfolio:CombatValue`, `EquipmentValue` | Useful progress weights, defaults 10/100 |
| `Portfolio:MoveSeconds`, `GatherSeconds`, `FightSeconds`, `RestSeconds`, `EquipmentSeconds` | Estimated cycle components, defaults 7/5/8/6/3 |
| `Telemetry:Endpoint` | OTLP HTTP/protobuf trace receiver, default `http://localhost:4318/v1/traces` |

The staged origin allowlist accepts loopback HTTP or exact official HTTPS origin `https://api.artifactsmmo.com`. Credentials in URLs, extra path/query/fragment and other origins are rejected. HTTP redirects are disabled in production registration; every request has a 30-second timeout. Live opt-in is a guard, not a claim that ADR 0001's supported combat subset is live-ready.

OneShot performs one portfolio tick and stops. If its response was lost it may perform one further read-only reconciliation tick, never a second action. Repeated calls on the same staged runner return its existing result. Legacy requires explicit action opt-in, retains the old mining/step pipeline, and is a compatibility mode rather than the staged safety proof.

## Local deterministic rollout

Prefer socket-free acceptance:

```text
dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~StrategyPortfolioFlowTests|FullyQualifiedName~StagedOperationTests"
```

For an intentional interactive mock run, start MockService using its documented profile, then reset its named scenario with `POST http://localhost:5000/__mock/reset` and body `{"scenario":"strategy-portfolio"}`. The profile below contains only fixed mock credentials. In PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'portfolio-mock'
dotnet run --project Artiact/Artiact.csproj --no-launch-profile
```

This inspects. To intentionally execute one mock action, restart with:

```text
dotnet run --project Artiact/Artiact.csproj --no-launch-profile -- --Execution:Mode=OneShot --Execution:AllowActions=true
```

The web process remains available for health/metrics after the staged worker finishes; one-shot does not imply process exit. Do not repeatedly restart a lost/unknown action without reviewing reconciliation. Across process restart there is no durable command journal.

## Health, drift and freshness

`GET /health/live` returns 200 when the web process answers. `/health/ready` and `/health` return 200 only after successful inspection, a verified one-shot action/completed target or successful read-only reconciliation, while probe and observation remain fresh. Otherwise they return 503 with state/reason, expected/observed successful API version, observation timestamp and fingerprint. They perform no API requests and contain no raw character state or credentials. Expired readiness does not silently trigger another action. Legacy does not publish staged readiness.

The compatibility probe checks the configured version, used GET/POST methods, equipment array request shapes, movement map ID and selected required character/map/fight/skill primitive/reference shapes. It is deliberately a subset guard, not a full schema diff or live mechanics proof. Invalid/missing JSON/schema, changed version and slow observations block. `ApiContractUnavailableOrDrift` and `StaleObservation` survive the final blocked decision in health output. Full raw catalog/character/policy fingerprints detect changed preflight state.

## Authentication, retry and cancellation

Basic authentication is request-local to `/token`; other requests use request-local Bearer. Token acquisition is serialized. A readable JWT `exp` schedules refresh with a 30-second margin; this is not signature validation. Opaque tokens refresh on a rejected read or after an action's 401 invalidates the cached token.

A GET has at most two dispatches total across authentication/transient failures. The first 401 refreshes; the first 429/5xx/network failure can retry. Valid Retry-After delta/date up to five seconds is honored, missing/invalid values use one second, and a longer requested delay returns the failure without retrying early. A second failure stops. Action POSTs never retry, including 401, 5xx and response loss.

Operation cancellation reaches GETs, token acquisition, semaphore waits and retry delays for concrete clients. Portfolio/combat and supported legacy steps establish this scope. Once an action POST is sent, the HTTP timeout bounds it; cancellation does not discard a successful returned snapshot. The snapshot is saved before cancellation stops subsequent work. Unknown outcomes remain fail-closed and require reconciliation.

## Cache and telemetry migration

Legacy `CacheService` now stores format-versioned envelopes under the OS local application data directory `Artiact/cache/<endpoint-version-hash>/`. Names hash the DTO type; identity hashes endpoint authority/version without credentials. Entries include creation UTC and expire after 48 hours by default. Future, expired, malformed, foreign-identity or unavailable entries are misses. Writes use unique temporary files and atomic replacement; contention retains the prior cache entry. Tracked `Artiact/cache` snapshots are neither refreshed nor used as a fallback by this service. New portfolio observation still reads current raw catalogs.

The deprecated/vulnerable Zipkin exporter was removed in favor of OTLP. Configure an OTLP collector/backend at `Telemetry:Endpoint`; the existing Compose Zipkin service does not directly receive this exporter. An external collector can bridge OTLP to Zipkin. The old `ZipkinSettings:Endpoint` key is no longer consumed. See the [upstream migration notice](https://opentelemetry.io/blog/2025/deprecating-zipkin-exporters/) and [Zipkin advisory](https://github.com/open-telemetry/opentelemetry-dotnet/security/advisories/GHSA-88hf-wf7h-7w4m).

## Release gates and remaining boundary

CI builds with warnings as errors, runs the full solution, explicit staged HTTP acceptance and separate RealApiOffline checks, and builds the Dockerfile from repository root without running its image. `.dockerignore` excludes secrets, caches and generated output. Use `docker build -f Artiact/Dockerfile -t artiact:local .` when Docker is available. Actual telemetry delivery, monitoring Compose, production deployment and a real-character action remain unverified; see dated epic evidence for exact commands.

Offline release completion does not clear the [combat ADR live no-go](decisions/0001-combat-viability-and-recovery.md). A real-character rollout requires separately approved character, policy, supported world state and action review. Bank, tasks, market, events and multi-character scheduling were not added.
