# Epic 6 execution evidence — 2026-09-06

## Craft-accounting prerequisites

Base `e7d1f56`. Replaced request-wide recursion history and per-branch stock copies with path-local cycle detection and a transactional shared working stock. Caller inventory is unchanged; batch surplus is reusable, real ingredients are consumed once. Reciprocal drop ranking now uses ascending positive rates with ordinal ties; nonpositive requested quantities fail before catalog access.

`dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~CraftConservationTests|FullyQualifiedName~TargetLootingResolverTests" --verbosity quiet` initially failed4/passed4: shared dependency, caller-stock mutation/double-use, and reciprocal ranking defects. Final focused suite passes16 including added surplus, partial intermediate, invalid/overflow, invalid-rate/request and tie coverage. Added coverage was already GREEN on the corrected implementation.

Final `dotnet build Artiact.sln --no-restore --verbosity quiet` passed; `dotnet test Artiact.sln --no-restore --verbosity quiet` passed392 application +101 mock, zero failed/skipped; `git diff --check` passed. Existing NU1902 remains. Independent review found no concrete implementation blocker in CraftChainBuilder blob7e7cabd and TargetLootingResolver blob158b8d9 against e7d1f56; it requested the additional boundary coverage above, supplied by the parent. Its earlier focused craft/finder check passed18. No complete combat loot/craft integration is claimed by this prerequisite slice. [Craft extension](craft-extension.md) defines that remaining scenario before implementation.

## Combat/equipment HTTP slice

Base `6e2b04f`; this slice adds presence-aware fire-only normalization, conservative predictor, explicit session factory/DI, finite one-command combat run, current-map catalog resolver, pre-owned gear comparison, response-validation port and scripted combat mock scenarios. Default autonomous mining remains unchanged. Loot/craft and Epics 7–8 are still pending.

RED/GREEN observations:

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~FightDefeatTests --verbosity quiet`: initial one failure confirmed two fight calls after loss; after fix one passes and worker defeat coverage passes.
- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~CombatPredictionTests --verbosity quiet`: initial conservative scaffold failed 6/passed 2; implemented predictor passes all 8.
- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~CombatObservationTests|FullyQualifiedName~CombatPredictionTests" --verbosity quiet`: observation scaffold failed the valid-state case, 19 already fail-closed/predictor cases passed; normalization then passes 20.
- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~CombatRunTests --verbosity quiet`: non-executing scaffold failed all initial 5; implementation passed all.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~CombatProgressionFlowTests --verbosity quiet`: initially both named reset requests failed; implemented HTTP scenarios pass baseline/gear acceptance with independent complete response oracles, exact decision replay and virtual times29/35.
- Independent review predicted half-finished swap and ignored malformed mock bodies. `dotnet test Artiact.sln --no-restore --filter "FullyQualifiedName~SwapCanEquip|FullyQualifiedName~MalformedAvailableAction" --verbosity quiet` reproduced all6 failures, then passed all6 after fixes.
- HTTP mutation/loss/cancellation test initially had a harness ordering error (GameHttpClient configured after first request), not behavioral RED. After fixing the harness, 8 cases passed and negative drop quantity failed; validating drop members made all9 pass.
- Existing reset regression with duplicate JSON properties was found by the broad gate and fixed using JsonDocument's duplicate-preserving property enumeration; focused reset/combat suite passed19.

Final verification:

- `dotnet build Artiact.sln --no-restore --verbosity quiet`: passed, zero errors, existing NU1902.
- `dotnet test Artiact.sln --no-restore --verbosity quiet`: 380 application +101 mock passed, zero failed/skipped.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline --verbosity quiet`:36 passed.
- `npx -y @fission-ai/openspec@1.12.0 validate --all --strict`:9 items passed, zero failed, existing long-requirement notices and environment TLS warning.
- `git diff --check`: passed.

Independent review covered the working diff against6e2b04f. Reviewer reran50 Combat/FightDefeat and21 real-client combat tests, confirmed both blockers resolved and found no new concrete blocker. Reviewed blob identities: CombatRun6f577e6, CombatActionPort5750b7c, CombatCatalog098877a, CombatScenarioStorecfebf9f, CombatProgressionFlowTests3f4f5f8, ExpectedCombat82a4bb0. The parent ran the broad gate above. No live/API safety proof, full emulator, optional systems or craft extension is claimed. Pre-existing `.serena/project.yml` remains excluded.

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

## Final loot/craft integration and Epic 6 acceptance

Base `3d300c5`, final working diff reviewed on 2026-09-06. Explicit crafting sessions reuse the shared-stock planner, verified opponent and workshop/skill constraints. The complete independent HTTP oracle and replay cover 14 decisions, 13 actions and 81 virtual seconds through loot, craft, equip, rest and combat level 3. Default worker behavior is unchanged.

The first new HTTP scenario test failed because combat-crafting was not implemented. Domain integration preceded that test; no blanket test-first claim is made. Independent review subsequently identified full-inventory craft rejection, unnecessary catalog access for completed/invalid goals, and division by zero for zero-yield nested recipes. `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~CombatCraftBoundaryTests` reproduced all four cases before fixes, then passed 4. Added HTTP missing/blocked workshop, insufficient skill and invalid recipe checks pass with zero actions. A timestamp formatting error in the test oracle after 60 seconds was corrected as a harness defect.

Final commands:

- `dotnet build Artiact.sln --no-restore --verbosity quiet`: passed, zero errors.
- `dotnet test Artiact.sln --no-restore --verbosity quiet`: 396 application + 107 mock tests passed, zero failed/skipped.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline --verbosity quiet`: 36 passed.
- `git diff --check`: passed.

Independent re-review against `3d300c5` confirmed every reported blocker resolved and found no remaining concrete Epic 6 acceptance blocker. Reviewer independently reran the 4 boundary and 27 CombatProgressionFlowTests. Reviewed blobs: CombatRun `fa6665f`, CombatSessionFactory `0019770`, WearCraftTargetFinder `5be90a3`, boundary tests `a605cd0`, flow tests `fd611bd`. Parent owns broad gates and documentation. Existing NU1902 warning remains for Epic 8 remediation. Live compatibility, production combat safety and optional systems remain unverified; ADR live no-go still applies. User tooling edit `.serena/project.yml` excluded.
Final `npx -y @fission-ai/openspec@1.12.0 validate --all --strict`: 9 passed, zero failed; existing long-requirement notices and environment TLS warning.
