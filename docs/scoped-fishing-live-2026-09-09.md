# Scoped fishing live trial — 2026-09-09

The user approved one run after reviewing fresh Inspect: gllab, move from map379 to map385, fishing level1→2, at most16 actions /20 decisions /3 consecutive no-progress /900 seconds. Preserve copper_ore2, sunflower40, event_ticket1. No bank/craft/combat or second milestone. This is general-development intermediate gathering acceptance, not a new needs-order chain.

RunId `gllab-fishing2-20260909-v1`; directory `%USERPROFILE%/.artiact-runs/gllab-fishing2-20260909-v1`. Existing directory contents refuse replay; never delete the marker or checkpoint. Previous alchemy trial remains retained. The isolated test uses existing production StagedExecution, cooldown, journal, preflight and reconciliation; no main-host startup or configuration changes. A fresh read-only matrix must select the exact fishing candidate/Move385 before dispatch. Transport pins that snapshot/world and checks every action, preserving all three initial stocks, latching on any failed/unknown reply or reaching fishing2. The production run retains the900-second budget; in-flight request/cooldown shutdown may extend wall time.

Offline gates: `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline` and `dotnet test Artiact.sln --no-restore`. The copied, separately named fishing guard retains the historical alchemy guard unchanged. Tests cover wrong character/action/map/body, stale/changed/incomplete observation, loss latch, reaching milestone,16-dispatch ceiling and sealing. No independent review is claimed.

Authorized invocation only, temporarily setting `ARTIACT_FISHING_LIVE_APPROVAL=gllab:fishing2:385:16:900:20260909-v1` and restoring its prior value in finally:

```text
dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --no-restore --filter Category=RealApiFishingLive --logger "console;verbosity=detailed"
```

The terminal must contain no pending intent and all verified entries; reopen with sealed transport must add no POST. Cancelled at the target is expected because the scope guard requests Stop. Retain that checkpoint; do not bypass archive rules. Exact results are appended after execution. The earlier R16 intermittent checkpoint cause remains unresolved; the current run preserves and reports any storage failure.
