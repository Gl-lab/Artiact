# R6 local execution evidence — 2026-09-07

Base: `3d4f10f34b1a600de4606095a1eb3f18cff5f49d`. Reviewed change: `run-lifecycle` application/session/checkpoint/CLI code, its three affected test files plus new process test, and documentation diff against that base. Review was self-review; no independent review is claimed.

## Behavior

Version-1 checkpoints retain optional initial/latest observations and terminal time without inventing missing historical facts. Offline result/archive commands run before host construction, retain exact archive bytes, reject pending/unsafe/mismatched completions, and prevent reuse of an archived identity. New runs remain explicit. Existing cancellation and unknown-outcome semantics remain terminal.

The extended real-client gathering-bank scenario reaches mining 4 / XP 0 through 13 commands and 71 confirmed cooldown seconds, with two deposits. It finishes with bank ore=4, inventory ore=2 and protected=1. Restoring the completed checkpoint and requesting cancellation adds no POST.

Full child-host tests use a loopback proxy to TestServer with sentinel credentials. Kill points: an intent whose POST has reached the proxy but has not been accepted by the backend; a backend-accepted action whose response is withheld; and a verified checkpoint during the actual seven-second cooldown. Restarts issue no second POST; the first case remains unknown, the second reconciles and reaches the preserved one-action budget, the third retains seven confirmed cooldown seconds. The precise interval after client deserialization but before result persistence remains covered by session-level injected storage failure (`RestartAfterReplyBeforeSaveOnlyReconciles`), not an instruction-level process-kill hook. This distinction limits the process evidence.

## Commands and results

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~DurableResultRetains|FullyQualifiedName~DurableTerminalTime"`: RED, 2 intended assertion failures before production edits (missing durable initial observation and terminal time).
- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~RunLifecycleTests|FullyQualifiedName~DurableRunTests"`: 25 passed. Archive command tests were added with implementation; no RED claim for that new API.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~GatheringBankConserves`: 1 passed; existing acceptance extended without a production behavior change.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~VerifiedRealClientCheckpoint`: 1 passed.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~RunProcessRecoveryTests`: 3 passed. Harness development first exposed a missing Xunit import and a Windows file-observation collision. Neither is claimed as a product RED result. The observer now reads during cooldown, not over atomic replacement.
- `dotnet test Artiact.sln --no-restore`: 494 application tests and 164 mock/process tests passed, 0 failures/skips.
- `git diff --check`: passed; Git reports local LF-to-CRLF conversion notices.

No live API calls, game actions, container rollout, separate RealApiOffline or combat research suite were run. Shared wire DTOs were not changed. The live bank/target protocol is prepared separately in `live-acceptance.md`; no live attainment is claimed. Process results above were verified on this Windows checkout; Linux/CI execution is not yet evidence.

The pre-existing roadmap/navigation edits are included as the user-authorized roadmap baseline. `.serena/project.yml` remains an unrelated user change and is excluded from publication.
