# Epic 8 execution evidence — 2026-09-06

Specification was independently reviewed, strictly validated, committed and pushed as `db2addc` before implementation. Scope is offline staged operation; real-character rollout is explicitly separate. The final implementation diff is against `db2addc`, excluding user-owned `.serena/project.yml`.

## Behavior and review

Transport tests first exposed missing refresh/retry behavior (7 failures). Cache API scaffolding failed 3 tests before envelope/TTL/atomic storage implementation. Nonexecuting staged runner scaffolding failed 14 cases before implementation. These are vertical RED/GREEN slices, not a claim that every DTO/probe/registration edit was test-first.

Independent review identified legacy authentication cancellation (2 reproduced failures) and overwritten drift/stale health reasons (5 reproduced failures). Both were fixed and their focused suites passed. Additional regressions detected during broader checks were successful in-flight action state lost on cancellation, Windows cache replacement contention, missing catalog data becoming an empty array (4 offline failures), and missing monster effects becoming an empty list (1 research failure). Fixes preserve authoritative action responses and nullable presence-sensitive DTO fields rather than weakening assertions.

Final independent code review found no remaining concrete blocker and independently ran 39 transport/contracts/cache tests, 14 staged HTTP tests and 36 RealApiOffline tests. Reviewed blobs against `db2addc`: Program `e53555c`, health `5dad8e9`, runner `32f34ca`, ActionResponse `638de65`, application dependencies `f9cabc5`, Dockerfile `6040059`, CI `1339133`. Parent subsequently removed the unused ZipkinSettings type and updated documentation. A final test dependency/documentation delta review is recorded below.

Zipkin was replaced with OTLP because the patched Zipkin package is deprecated; no obsolete/advisory suppression was added. A direct NuGet.org audit found two additional old transitive packages through xunit 2.5.3 that the configured proxy audit missed: [System.Net.Http advisory](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) and [Regex advisory](https://github.com/advisories/GHSA-cmhx-cq75-c4mj). Updating the two solution test projects to xunit 2.9.3 removed that dependency chain; the existing adapter still discovers and passes every test.

## Final commands on implementation diff

- `dotnet restore Artiact.sln`: passed after test package update.
- `dotnet build Artiact.sln --no-restore --verbosity quiet -t:Rebuild --warnaserror`: passed, zero warnings/errors, including after final test package update.
- `dotnet test Artiact.sln --no-restore --verbosity quiet`: passed 436 application + 136 mock, zero failed/skipped, including after final test package update.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline --verbosity quiet`: passed 36, zero failed/skipped, on final production code.
- `dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore --verbosity quiet`: passed 83, zero failed/skipped, no warnings, on final production code.
- `dotnet list Artiact.sln package --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json`: no vulnerable packages reported in all five solution projects after xunit update.
- `npx -y @fission-ai/openspec@1.12.0 validate --all --strict`: 11 passed, zero failed. Informational long-requirement notices and a pre-existing environment `NODE_TLS_REJECT_UNAUTHORIZED=0` warning were emitted; environment configuration was not changed.

No main host, live test, real-character action, secret read or tracked cache refresh was performed. Docker and gh are unavailable locally. The Docker build is configured in CI but local container build/execution, actual telemetry delivery, monitoring Compose and deployed health routing remain unverified. The authored mock OpenAPI subset is not full upstream compatibility evidence. Durable reconciliation across restart and broader combat mechanics remain outside this scope.

Final independent delta review accepted xunit 2.9.3 with the unchanged adapter and found one stale limitations paragraph. It was corrected to distinguish concrete GET/auth cancellation and 30-second HTTP bounds, in-session portfolio reconciliation, and custom tokenless/Legacy/cross-process limits. Previous code approval retained. The final focused HTTP command dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter 'FullyQualifiedName~StrategyPortfolioFlowTests|FullyQualifiedName~StagedOperationTests' --verbosity quiet passed 29, repeating deterministic portfolio/replay acceptance after the full suite. git diff --check passed after documentation whitespace cleanup.
