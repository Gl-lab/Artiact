# R13 verification — 2026-09-08

Reviewed base: `cb230bd` plus the R13 diff in Strategy, Operation, the recovery-training mock, unit/socket-free tests and documentation. Self-review only. The user's `.serena/project.yml` is excluded.

Compiling RED evidence: configuration ignored Recovery before policy binding; the new recovery-training reset returned 404 before its fixture existed; final productive-use assertions reported NoProgress 3/9/9 instead of zero before confirmed healing was marked productive. The training-level prediction was corrected transparently in comparisons.md; no fixture, utility or cost ceiling was relaxed.

Verification commands and results:

- `dotnet test Artiact.Tests --no-restore --filter FullyQualifiedName~AutonomousRecoveryTests`: 10 passed during implementation.
- `dotnet test Artiact.MockService.Tests --no-restore --filter FullyQualifiedName~RecoveryUsesBankOrOneFiniteProductionChainWithoutRefill`: three intended RED failures before the productive-use correction; all three pass in the final solution gate.
- `dotnet test Artiact.MockService.Tests --no-restore --filter FullyQualifiedName~TerminalRunReportsArchivesAndCannotRefundBudget`: 2 passed; repeated with `--no-build` twenty times, all passed.
- `dotnet test Artiact.sln --no-restore`: 553 application + 257 mock tests passed, zero skipped. Includes accumulated comparisons with replay and manual/autonomous process recovery.
- `dotnet test Artiact.MockService.Tests --no-build --no-restore`: five additional complete replays, 257 passed each, no failures; repeated to investigate the intermittent checkpoint failure.
- `git -c core.safecrlf=false diff --check`: passed.

One preceding full run failed the R12 two-action file-reopen case (CheckpointUnavailableOrInvalid), with 553 application and 256 mock passes. The similar R12 transient is retained in its evidence. Expanded test-only diagnostics capture store errors and checkpoint facts; the underlying cause is not established. A passing repeat does not prove its absence.

Review covered default-disabled permissions and stable absent-property identity, finite deficit utility, global charges, retained stock without bank access, whole required bank withdrawal, pending Use reconstruction, observed HP completion, supply vs useful progress, schema permission boundaries, cancellation and invalid postconditions. Comparisons report ties for training/production and fewer commands/cooldown for ready-bank recovery; lowest-skill does not restore HP. Existing R12 ceilings remain satisfied. Architecture diagrams retain the existing observer/session/journal flow; no new runtime service or wire contract was introduced.

Unverified: live gameplay, live combat readiness and RealApiOffline (no DTO or route additions). Full-path training and future thresholds remain bounded estimates; this is the supported deterministic recovery subset, not arbitrary recipe feasibility or optimality. R14/R15 remain separate epics.
