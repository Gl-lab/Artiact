# Scoped fishing live trial — 2026-09-09

The user approved one run after reviewing fresh Inspect: gllab, move from map379 to map385, fishing level1→2, at most16 actions /20 decisions /3 consecutive no-progress /900 seconds. Preserve copper_ore2, sunflower40, event_ticket1. No bank/craft/combat or second milestone. This is general-development intermediate gathering acceptance, not a new needs-order chain.

RunId `gllab-fishing2-20260909-v1`; directory `%USERPROFILE%/.artiact-runs/gllab-fishing2-20260909-v1`. Existing directory contents refuse replay; never delete the marker or checkpoint. Previous alchemy trial remains retained. The isolated test uses existing production StagedExecution, cooldown, journal, preflight and reconciliation; no main-host startup or configuration changes. A fresh read-only matrix must select the exact fishing candidate/Move385 before dispatch. Transport pins that snapshot/world and checks every action, preserving all three initial stocks, latching on any failed/unknown reply or reaching fishing2. The production run retains the900-second budget; in-flight request/cooldown shutdown may extend wall time.

Offline gates: `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline` and `dotnet test Artiact.sln --no-restore`. The copied, separately named fishing guard retains the historical alchemy guard unchanged. Tests cover wrong character/action/map/body, stale/changed/incomplete observation, loss latch, reaching milestone,16-dispatch ceiling and sealing. No independent review is claimed.

Authorized invocation only, temporarily setting `ARTIACT_FISHING_LIVE_APPROVAL=gllab:fishing2:385:16:900:20260909-v1` and restoring its prior value in finally:

```text
dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --no-restore --filter Category=RealApiFishingLive --logger "console;verbosity=detailed"
```

The terminal must contain no pending intent and all verified entries; reopen with sealed transport must add no POST. Cancelled at the target is expected because the scope guard requests Stop. Retain that checkpoint; do not bypass archive rules. Exact results are appended after execution. The earlier R16 intermittent checkpoint cause remains unresolved; the current run preserves and reports any storage failure.

## Observed result

Reviewed/executed revision: `a7c28b3` (parent `ea14568`). Self-review compared the new guard directly with historical ScopedAutonomousTransport; only fishing/map scope and additional protected stocks differ. Pre-execution gates passed: solution636+331 tests; RealApiOffline89 tests; staged diff check clean. The user explicitly approved the route before invocation. The opt-in environment value was restored in finally.

Fresh Inspect at2026-09-09 05:38:54 UTC: fingerprint `9ADF45294D66E0C9E94DCEFFBA95DCA3BB0A891CD0C1F7924D2184203592D951`, world `5740BD156AC918FED875022034DAD66C42BB185923282965CFB3CD5D712FD01D`, policy digest `A111FD175E849243A2F129D63D6EF12B0F458FB4E1A626C111B023DCBA1F7511`;43GET,18.1566389s acquisition; candidate `skill:fishing:gudgeon_spot:intermediate`, first command Move385, free57/100. No game POST in Inspect.

Bounded session:05:38:59.245–05:47:04.500 UTC (485.255s). Eight verified actions: Move385 and seven Gather:fishing. Fishing remains level1,91/150XP. Gained gudgeon7, shell1, algae1; copper_ore2/sunflower40/event_ticket1 unchanged. Verified cooldown220s (move10 plus seven gathers30). Journal attempts9/decisions9/no-progress1: eight Verified entries and one locally Rejected Gather intent. No pending or unknown result.

The ninth dispatch was blocked by the pinned-world condition before transport send. Only map523 changed between saved initial/latest catalogs: Forest content monster/demon became null. Maps count1428, resources26 and items524 were unchanged in count; resource/item contents were unchanged. Latest world became `B67B4E1945F13CF5CF2E5B033EE78182863BC6C993DD498757CCE4E721428000`. This was an unrelated event change, not an observed change to fishing map385. The guard deliberately did not accept the new world. No additional action or replacement run was attempted.

Live test result: FAIL because it required every journal entry Verified and level2; the intended milestone was not reached. This is evidence of eight real verified actions and a conservative stop, not full milestone acceptance. Terminal is Blocked/Rejected, CanArchive=false; marker/checkpoint retained outside Git.

Follow-up command `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=FishingResultReview --logger "console;verbosity=detailed"` passed1 test. It reads the retained checkpoint then reopens it in the actual StrategySession over an in-memory store and a throwing observer: terminal/counters are identical without observation, network, dispatch or rewriting the retained checkpoint. The original live test had stopped at its all-Verified assertion before its own reopen check; this separate offline check covers terminal replay only.

Limitation exposed: the acceptance transport pins the entire map catalog, so unrelated dynamic events can stop a long gathering run. A future route-scoped guard would require a separately specified/tested change; this run did not weaken that boundary. No bank, craft, combat, new needs-order chain, level2 completion or independent review is claimed.