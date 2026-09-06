# Epic 6 execution evidence — 2026-09-06

## Transport and action envelope slice

Implementation base: specification commit `4f5e736`, following production/research base `05f7944`. Scope: single-dispatch action client, terminal worker failure handling, failed token guard, exact-name fight snapshot adapter, named equipment arrays and retained fight/rest/equipment details. Not the complete combat epic.

Source: public GET of https://api.artifactsmmo.com/openapi.json, version 8.2.3, inspected CharacterFightDataSchema, CharacterFightSchema, EquipmentTransactionSchema, EquipSchema, UnequipSchema and CharacterRestDataSchema. No authentication or live action performed.

RED evidence: `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~SingleDispatchTests --verbosity quiet` failed 9 and passed 1 before production transport changes. Failures demonstrate duplicate dispatch, response-body disclosure, missing response-state acceptance and action after authentication rejection. The null envelope was already rejected. After implementation all 10 passed. Worker terminal tests were added after the change and were already GREEN; no worker RED claim is made.

`dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~CombatContractTests --verbosity quiet` failed the controlled-participant success case and passed 3 fail-closed cases before fight adaptation. After adaptation and equipment/rest coverage, the solution passes.

Commands on the final slice code:

- `dotnet build Artiact.sln --no-restore --verbosity quiet`: success, zero errors; incremental build reports pre-existing NU1902 for Zipkin 1.12.0. Initial baseline build also reported existing nullable, StepBuilder and ASP0000 warnings.
- `dotnet test Artiact.sln --no-restore --verbosity quiet`: 336 application tests and 80 MockService tests passed, zero failed/skipped. Baseline was 317 + 80.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline --verbosity quiet`: 36 passed, zero failed/skipped.
- `dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore --verbosity quiet`: 83 passed, zero failed/skipped.
- `npx -y @fission-ai/openspec@1.12.0 validate add-bounded-combat-progression --strict`: passed at specification commit. Environment reports NODE_TLS_REJECT_UNAUTHORIZED=0; not changed by this work.
- `git diff --check`: passed after removing trailing whitespace in the touched worker.

Review: self-review of the working diff against `4f5e736`, including new exception, DTO and client tests. No independent review is claimed. No other equipment callers or Moq setups exist; interface and implementation migrated together. Existing mock fixtures/cache files unchanged. Pre-existing `.serena/project.yml` modification excluded.

Not verified/remaining: complete required combat stats, exact fight-result participant details, cooldown domains, map identity/access, defeat handling in legacy steps, bounded combat policy, full independent HTTP scenarios, loot/craft extension and live rollout. Equipment/result item details are retained as JSON for subsequent normalization. Existing research probes remain historical fragments, not full API compatibility evidence. Epic 6 tasks remain open until these are implemented and verified; Epics 7–8 have not started.
