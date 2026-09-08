# R24 acceptance — 2026-09-08

Base `7eadfe3`; self-reviewed this commit's anonymized regression and acceptance documentation. No production behavior changes. New test was GREEN against completed R21–R23, as expected for acceptance; no artificial RED or independent review claimed.

## Deterministic and UI evidence

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~AnonymizedInventoryPressure`: 1 passed. Free 57, nearest estimated 77, deficit 20, zero attempts.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter 'FullyQualifiedName~OperatorCycle|FullyQualifiedName~Intermediate|FullyQualifiedName~GatheringBankConserves'`: 4 passed. Real clients/TestServer cover operator Inspect/start/stop/reopen, intermediate restart and conserved bank route.
- Actual repository panel.html/panel.js/panel.css served by a temporary Node HTTP server on loopback 5089 with synthetic responses only. In-app browser opened saved mixed refusal, expanded controls, clicked Inspect, displayed numeric evidence and original milestone, and reloaded the saved result. Screenshot reviewed at approximately 1004px width: no overlapping text/controls, alternatives collapsed, stale label and unavailable Stop visible. Server stopped after inspection. Initial browser-selection timeout resolved by browser inventory and explicit available browser handle.
- `node --test Artiact.Tests/operator-panel.test.cjs`: 17 passed in R23 final gate; assets unchanged in R24. Covers refused start, duplicate clicks, stale/unknown/missing diagnostics.
- `dotnet restore Artiact.RealApiTests/Artiact.RealApiTests.csproj`: succeeded.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 74 passed.
- `dotnet test Artiact.sln --no-restore`: 630 application + 303 MockService tests passed on final code.

## Read-only API

Command: `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiBudgetAssessment --logger 'console;verbosity=detailed'`, temporary `ARTIACT_REAL_API_READONLY=1`, previous value restored in finally. Executed twice because initial verbose output was truncated; second output compacted in memory without persisting secrets/logs. Each invocation acquired one snapshot and assessed all six rows locally. Both passed; no game POST, bank read, cache refresh or setting change.

Final snapshot at **2026-09-08 19:34:15 UTC**; 43 GET requests, 20.270884 seconds acquisition, capacity 100/free 57. Fingerprint `344E9E19C4AE26FE2ECB7651BB2BC2319331F28B72423F58F31B8978D844493A`; world `C821C427E620876DB175712929A2E8918B3C282C4D849C72D866D32DCCB8CC57`. Same fingerprints as the earlier 19:31:14 snapshot. Bank disabled. All rows use 20 decisions, MaxNoProgress 3, zero attempts.

| Actions | Seconds | Status/reason | Proposed first command |
|---|---|---|---|
| 2 | 120 | Stopped / AutonomousBudgetExhausted | none |
| 12 | 600 | Stopped / AutonomousBudgetExhausted | none |
| 13 | 120 | Stopped / AutonomousBudgetExhausted | none |
| 13 | 134 | Selected / InspectOnly | Move:385 |
| 16 | 180 | Selected / InspectOnly | Move:385 |
| 16 | 600 | Selected / InspectOnly | Move:385 |

Full unlock paths remain inventory-rejected. Nearest fishing/woodcutting level 2 fits at sufficient action/time budget; nearest mining remains inventory-rejected. No acceptance row authorizes execution. Live bank and R25 chains remain separate unverified scope.
