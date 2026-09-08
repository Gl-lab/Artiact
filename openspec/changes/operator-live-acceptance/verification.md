# R19 preparation and unsuccessful read-only preflight — 2026-09-08

Reviewed base `a5d5352` plus small decision-budget compatibility, explicit autonomous inspection scope, tests and docs. Self-review only; no independent agent. No shared Contracts changes. Existing manual Inspect behavior and default read allowlist remain unchanged.

Compiling REDs:

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~SixDecisionProtocolDoesNotRequireIncreasingLiveBudget`: failed ProfileUnavailableOrInvalid before lowering the arbitrary minimum; passed after the change. Zero and no-progress > decisions remain invalid.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter "Category=RealApiOffline&FullyQualifiedName~AutonomousItemsRequireExplicitReadScopeAndNeverAllowPosts"`: failed the explicit items read before enabling it; default items access, all action POSTs, other-character reads and query injection remain denied.

`dotnet restore Artiact.RealApiTests/Artiact.RealApiTests.csproj`: up to date. `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 55 passed before the live read. Final gates are recorded below.

The following read-only command was executed once, preserving/restoring the previous process environment value in a PowerShell try/finally:

```powershell
$priorReadOnlyFlag = [Environment]::GetEnvironmentVariable('ARTIACT_REAL_API_READONLY')
try {
    $env:ARTIACT_REAL_API_READONLY = '1'
    dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --no-restore --filter Category=RealApiAutonomousInspect --logger 'console;verbosity=detailed'
} finally {
    [Environment]::SetEnvironmentVariable('ARTIACT_REAL_API_READONLY', $priorReadOnlyFlag)
}
```

Result: **1 failed actionable-acceptance assertion**, reason AutonomousBudgetExhausted, Attempts=0, Decisions=0, CooldownSeconds=0, Command=null. The guard allowed authentication and read-only catalog/character access only; no game POST and no execution checkpoint was created. No bounds, category or character were changed to seek a passing result.

Observed at `2026-09-08T12:38:42.587136Z`: character `gllab`, map 277, capacity 100, free units 98, stock copper_ore=2. Observation fingerprint `13D88836A152ACD94031164B9167CBAA879D42C3A80AE5E9128C41D54CA02488`; world fingerprint `5B26B5CE443FE734ADBE9AD87E2B35D8534CB3235991B7686F4C3D21AD16C273`; policy digest `E985BBDD932F034F1FFB39BB53DF66D4C71185B0E322876582BF0FE5455CF50A`. These are dated evidence, not reusable freshness/approval.

Action scope remained 2 / decisions 6 / no-progress 3 / seconds 120. Catalog-derived paths included EstimatedPathExceedsBudget and EstimatedInventoryInsufficient, alongside unsupported resources. The alchemy target-2 estimate alone had remaining150 XP, expected13 XP/action and travel7 seconds; the current two-action budget cannot complete it. This is an estimate, not a proposed approved action or measured live cost.

The executed test initially printed the full decision; only its logger projection was subsequently shortened to rejection counts/path facts. Planning, bounds and transport behavior were unchanged. No repeat live read was used to manufacture GREEN.

Open R19 tasks: actionable concrete scope, R16 operational prerequisite/explicit scope decision, exact approved command transport and user authorization, real UI execution/stop/result, terminal restart and cost/stock evidence. R19 is not closed and R20 has not started. A scope question was sent to the user; absence of an answer is not permission to increase limits or act.

Subsequent update: the user explicitly requested limit reassessment. [Separate specification and evidence](../reassess-live-budget/verification.md) preserve this failed baseline and propose 16 actions/20 decisions/3 no-progress/900 seconds from a fresh read-only matrix and measured acquisition overhead. This authorizes preparation changes, not gameplay; the remaining live acceptance gates above still apply.

Final `dotnet test Artiact.sln --no-restore`: **576 application + 288 mock passed**, zero failures/skips. Final `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: **55 passed**, zero failures/skips. `git -c core.safecrlf=false diff --check`: passed. Review covered positive/inverted budget validation, unchanged manual read scope, game POST rejection, compact report projection, no game execution/checkpoint in inspection and restored environment guard. Updated bounded-operation, operator-panel, development, live-protocol and known-limitations documentation; runtime diagram structure is unchanged by this preparation.

Preparation published as `e1e1c9f`, pushed to origin/master. This publication does not close live acceptance. The user asked for an explanation of preflight; the explanation was given, and no expanded scope or game-action approval was inferred from that question.
