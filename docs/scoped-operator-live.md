# Prepared R19 operator field trial

Status: implementation prepared; live actions require the concrete approval below. This document is a proposed field trial, not evidence that R19 passed. The R16 intermittent checkpoint cause remains open.

Scope is `gllab` at `https://api.artifactsmmo.com/`: initial map277 and copper_ore2, Move:379 followed by bodyless gathering at379, alchemy level1→2. No other character/action/map is allowed. Ceilings:16 actions,20 decisions,3 consecutive no-progress,900 seconds. Stop earlier on the target level, user Stop, drift, rejection or uncertainty. Preserve at least two copper_ore; no bank/combat/crafting. See [budget calculation](../openspec/changes/reassess-live-budget/verification.md).

`ScopedAutonomousTransport` independently pins the initial character and catalog/policy fingerprints from the read-only evidence. Production sessions also validate fresh preflight and contract version. Every action needs a fresh complete observation, including all pages of maps/resources/items; changing the world or initial snapshot rejects the action. A lost/failed reply latches dispatch off. Reaching alchemy2 requests the operator stop token; production validation/journaling occurs before cancellation is recorded. A Cancelled checkpoint remains retained and is not automatically archived.

The isolated test host serves the real panel and coordinator at `http://127.0.0.1:5189/operator`. It authenticates with the guarded verifier, keeps credentials out of the host, and uses shared StagedExecution, StrategySessionFactory and FileRunCheckpointStore. It does not start actions automatically. It accepts exactly RunId `r19-gllab-alchemy2-v1` and the limits above through the panel. The directory is `%USERPROFILE%\.artiact-runs\r19-gllab-alchemy2-v1`, outside Git; any existing contents refuse another harness invocation. `host-started.intent` survives failures; never remove it to rerun. Repeated fields or Start requests do not authorize a second trial.

Before invocation, record the reviewed commit, refresh read-only evidence, and obtain approval covering this character/route, limits, stock floor and the explicitly open R16 criterion. The exact opt-in value is `ARTIACT_OPERATOR_LIVE_APPROVAL=gllab:alchemy2:16:900:R19-v1`. Only after approval, temporarily set it and run:

```text
dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --no-restore --filter Category=RealApiOperatorLive --logger 'console;verbosity=detailed'
```

Quote `console;verbosity=detailed` as one argument in PowerShell and restore the previous opt-in value in `finally`. Never run the separate project without a category filter. The harness waits up to30 minutes for the UI cycle; the action run retains its separate900-second budget. Host shutdown requests Stop and waits for shared execution. Use the panel for Inspect, review Move:379, Start, observation and Stop; the scope guard also stops automatically on alchemy2.

After the worker finishes, the harness permanently disables its transport, requires a durable terminal checkpoint without pending intent, reopens through shared execution, and checks unchanged dispatch count and verified journal entries. Unknown outcomes fail acceptance and retain state. It never clears or archives the checkpoint. Use the offline `run-result <absolute directory> https://api.artifactsmmo.com/gllab` command afterward to review actual XP/stock/cooldown and preserve sanitized evidence. Raw authenticated responses must not be committed or printed in the conversation.

Offline checks are `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline` and the solution gate. They verify transport boundaries and actual loopback panel startup, with sentinel data and no production requests. They do not establish real cooldown, XP, successful milestone completion or the complete live operator cycle.
