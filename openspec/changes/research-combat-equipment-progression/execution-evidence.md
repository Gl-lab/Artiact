# Epic 5 execution evidence — 2026-09-06

## Scope and baseline

Base revision: `4b49b1e19c4426aca9bcee8ac2f8d9cbebdc3888`, branch master. At start, this proposal directory was untracked and `.serena/project.yml` had an unrelated edit. `git fetch origin` and `git rev-list --left-right --count HEAD...origin/master` returned 0/0. Baseline `dotnet test Artiact.sln --no-restore` passed 317 application + 80 mock tests, zero failed/skipped, with existing NU1902.

Scope: all research tasks, an isolated .NET 9 xUnit experiment, current-schema fragment probes using unchanged Contracts, final ADR and Epic 6 handoff. The root instructions apply to the new experiment; no existing nested project is edited. Current client/DTO/loot/decomposition/step and characterization tests were inspected. Production source, solution membership, DI, existing test projects, caches and predecessor changes remain unchanged.

Public OpenAPI 8.2.3 was inspected and rechecked after implementation with identical byte hash `8AD21FBCCBB04CD6568BA7C0F4FE4FEF7EAAD6984763BB7EBE780FED8267F96D`. Sources and unresolved mechanics are recorded in the dated contract matrix. No authenticated API, main host, paid simulator or real-character action was invoked.

## Observed development checks

1. Authored protocol and independent predictor tests before its implementation. `dotnet restore openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj` succeeded. `dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore --filter FullyQualifiedName~PredictorTests` compiled and failed 13 cases, passed 8. The initial scaffold returned Unknown/zero damage: this is a new-model RED, not a reproduced production bug. Invalid/unsupported cases were already GREEN with that conservative scaffold.
2. Authored transition tests before its implementation. `dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore --filter FullyQualifiedName~CombatRunTests` compiled and failed all 39 cases against the non-executing scaffold. The first complete implementation passed all 60 predictor/transition cases. Scaffold-only CS9113 warnings disappeared after implementation.
3. Added independent payload probes and a participant-reader scaffold. `dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore --filter FullyQualifiedName~PayloadProbeTests` failed 3 participant-reader cases and passed 12 compatibility/malformed-input cases. Correct participant extraction then passed. Existing DTO shape-gap probes were already GREEN and deliberately characterize incompatibility; production was not changed to manufacture RED.
4. Seven supplemental transition cases (lost fight, successful swap, prerequisite budgets, partial rest, wrong gear postconditions, invalid cooldown; theory expansion included) were first executed after the corresponding implementation and passed. This is acceptance expansion, not a claimed test-first RED. The combined suite reached 82 passing cases.
5. Self-review predicted an unsafe gear branch on unknown current stats. `dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore --filter FullyQualifiedName~EquipmentCannotBypassUnknownCurrentState` failed: expected no commands, observed Unequip and Equip. Added the supported-current-state guard before equipment selection. The complete experiment suite then passed 83 cases.

The above explicitly records test-first exceptions. No missing-type compilation failure, rollback of working code or failed live call is counted as RED evidence.

## Acceptance and review

Review type: self-review of the complete candidate source/spec/docs diff, independent literal oracles, requirement scenarios and failure paths. No independent review claimed. The reproduced gear-guard blocker is resolved; no research blocker remains open.

Reviewed candidate manifest SHA-256: `6491B89826BB5BC3F08D456F4572BAF566C7431559D3987AB98B957CC229A4EA`. Algorithm: sort staged changed paths except this execution-evidence.md, join `<path> <staged Git blob ID>` lines with LF and no trailing newline, hash UTF-8 bytes. Blob IDs are obtained with `git rev-parse :<path>` and identify normalized staged content. This excludes the evidence file to avoid self-reference and includes all other 22 candidate files. The evidence file itself was separately reviewed for command/result accuracy.

| Requirement group | Evidence |
|---|---|
| Traceable contracts/mechanics | contract-matrix.md, mechanics.md, source hash recheck; explicit rest-source conflict and map/drop migration gaps |
| Alternatives and safety boundary | comparison.md and ADR 0001; same-fixture comparison without invented simulator/calibration measurements |
| Independent viability oracles | PredictorTests: HP/critical boundary, rounding, initiative, zero damage, turn cap, unsupported/missing/out-of-domain inputs |
| Finite transitions | CombatRunTests: literal golden five-decision/four-command/29-second outcome and seven-decision gear extension; terminal/config/counter/recovery/failure cases |
| State/cancellation/unknown outcome | Wrong map/stats/inventory, defeat relocation, rest/gear failures, before/after cancellation, single lost fight dispatch and sticky terminal results |
| Schema probes | PayloadProbeTests: actual legacy DTO field loss/request mismatch, exact-name participant extraction and malformed/duplicate/missing participant rejection |
| Replay | Every deterministic fixture repeats in-process using fresh state and checks independent expectations; full experiment command also repeated after corrections |
| Handoff | ADR go for separately specified deterministic Epic 6; no-go for live rollout; explicit remaining loot/craft slice and external blockers |

Docs README and development guide link the research and standalone command. Known limitations adds inspected compatibility findings without claiming fixes. Architecture/domain diagrams, existing nested AGENTS.md and mining guides were checked for impact: production flow is unchanged, so no diagram/contract/DI migration was needed. The experiment README describes its hardcoded destination, scripted conditions and partial payload scope.

## Final commands

| Command | Result |
|---|---|
| `dotnet build Artiact.sln --no-restore` | Passed, zero errors; existing NU1902 warning |
| `dotnet test Artiact.sln --no-restore` | Passed 317 application + 80 mock, zero failed/skipped |
| `dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore` | Passed 83, zero failed/skipped; no experiment compiler warnings |
| `npx -y @fission-ai/openspec@1.12.0 validate research-combat-equipment-progression --strict` | Passed |
| `npx -y @fission-ai/openspec@1.12.0 validate --all --strict` | Eight items passed, zero failed; pre-existing long-requirement notices |
| `npx -y @fission-ai/openspec@1.12.0 instructions apply --change research-combat-equipment-progression --json` | all_done; 19 complete, zero remaining |
| `git diff --cached --check` | Passed for all staged research/planning/docs files; no generated files or unrelated Serena changes staged |
| `git diff --exit-code HEAD -- Artiact Artiact.Contracts Artiact.MockService Artiact.Tests Artiact.MockService.Tests Artiact.sln` | Passed; existing runtime/projects unchanged |
| PowerShell relative Markdown-link scan over `git diff --cached --name-only` | Passed; every non-URL/non-anchor relative link resolves |

OpenSpec also reports the pre-existing NODE_TLS_REJECT_UNAUTHORIZED=0 environment warning; no environment change was made. Unchanged Zipkin dependency produces NU1902. No new production compiler warning is introduced.

An initial working-tree whitespace check with `core.autocrlf=false` misclassified CRLF line endings across tracked files. The publication gate uses the normalized staged diff with the repository's normal Git settings and passes; no unrelated line-ending rewrite was made.

## Unverified scope and publication

Not verified: real HTTP combat/rest/equipment payloads, full schema-valid response acceptance, paid simulation, observed combat calibration, live safety, upstream timing conflict, full effects, pathfinding, main-host execution, durable recovery or production rollout. Scripted unit tests do not prove these. RealApiOffline was not required or run because its code and shared DTOs were not changed. Contracts is referenced read-only for deserialization probes.

All task boxes refer to completed research, not Epic 6 implementation. Existing OpenSpec changes remain unarchived; this research change also remains available with its experiments. User-authorized commit and push are the final publication steps; the resulting commit and push outcome are reported in the final handoff.

Unrelated `.serena/project.yml` SHA-256 before/after remains `55EFEC5F37D514A138669A1573852ABA1FE90E6EE023C3601943C91B8A905898`; it is excluded from staging/publication.
