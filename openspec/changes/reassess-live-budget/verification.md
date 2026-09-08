# Budget reassessment — 2026-09-08

Base/review scope: `462f5d9` plus this change's RealApiTests helper, verifier/category and documentation diff. Self-review only. No production runtime, defaults, planner formula, cache, credential or action-permission changes. Existing `.serena/project.yml` changes excluded.

## Fresh read-only result

Explicit user request: «Нужна переоценка лимита». One acquisition at 2026-09-08T12:50:31.4998058Z, official origin, character gllab, map277, capacity100/free98, copper_ore2. Authentication plus allowlisted GET only; zero game attempts, no run checkpoint.

- Character fingerprint: `13D88836A152ACD94031164B9167CBAA879D42C3A80AE5E9128C41D54CA02488`.
- World fingerprint: `5B26B5CE443FE734ADBE9AD87E2B35D8534CB3235991B7686F4C3D21AD16C273`.
- Policy digest: `E985BBDD932F034F1FFB39BB53DF66D4C71185B0E322876582BF0FE5455CF50A`.
- Acquisition elapsed15.3247741 seconds; 43 GETs. Includes auth/planning; subsequent matrix evaluations use frozen observation without reads.

All rows use 20 decisions, three no-progress decisions, same snapshot and production discovery/selection; attempts0 throughout:

| Actions | Seconds | Result | First command |
|---|---|---|---|
| 2 | 120 | AutonomousBudgetExhausted | none |
| 12 | 600 | AutonomousBudgetExhausted | none |
| 13 | 120 | AutonomousBudgetExhausted | none |
| 13 | 134 | Selected / InspectOnly | Move:379 |
| 16 | 180 | Selected / InspectOnly | Move:379 |
| 16 | 600 | Selected / InspectOnly | Move:379 |

Alchemy sunflower_field target2: remaining150 XP, expected13 XP per gather => 12 gathers + one move. Discovery computes (12*5+7)*2 =134 configured seconds. Selected candidates' reported Path.UnitSeconds becomes10 after measurement adjustment; do not apply the multiplier a second time to that reported value. Fishing/mining/woodcutting target10 still fail estimated inventory; increasing time does not resolve them.

Proposed scope: 16 actions, 20 decisions, three consecutive no-progress, 900 seconds. Three action slots above the estimated minimum; four decision slots above the action ceiling. Expected upper-scope configured cooldown allowance (15*5+7)*2 =164 seconds; up to 36 observations (two per action plus four spare decisions) at15.325 seconds adds551.7, total715.7; rounding to900 leaves184.3 seconds for variation/action HTTP/local work. The 900-second selection result is a monotonic inference from the 600-second Selected row, not another live/matrix measurement. A single latency sample and configured XP/cooldown estimates do not guarantee completion. Stop at alchemy2 even with remaining allowance; prepare its guard with the concrete live transport before approval.

## Verification

TDD: `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter FullyQualifiedName~InspectionBudgetMatrixTests` compiled, then failed the missing-row assertion against the empty matrix scaffold (expected2/actual0). Implemented local sessions; the same test passes in the offline gate, checking distinct remaining budgets, original-context preservation and selected command non-dispatch.

`dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 56 passed before acquisition; final repeat recorded below.

Read-only command: temporarily set ARTIACT_REAL_API_READONLY=1 in PowerShell try/finally, restoring its prior value; `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --no-restore --filter Category=RealApiBudgetAssessment --logger 'console;verbosity=detailed'`: 1 passed, six rows reported, 21.398 seconds test-run time. This passing test verifies inspection completion, not action execution or milestone success.

Final gates: `dotnet test Artiact.sln --no-restore` passed 576 application +288 mock tests; `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline` passed56. No build/test warnings. `git diff --check` passed (Git reported routine LF-to-CRLF normalization notices). Final self-review covered all new helper/test/spec files plus the tracked diff: no blockers found. The network-read invariant was added after live acquisition and covered by the final compile/offline gate; no second live acquisition was performed for that assertion.

Unverified: real move/gather cooldown and XP, live operator cycle, scoped transport/milestone guard, durable live restart and R16 transient cause. Fresh pre-action observation and concrete live authorization still required; R19/R20 remain open.
