# R18: Explicit local run control

Base `ebe20d6`. Add preparation/Inspect, explicit start, same-run recovery, stop and guarded archive to the local panel. Reuse StagedExecution and FileRunCheckpointStore lifecycle rules used by the CLI. Do not implement a second planner or action transport. R16 historical diagnosis remains open by explicit user decision.

Scope: one local noncombat server-configured portfolio, editable finite limits bounded by server configuration, explicit RunId. Operator:ControlsEnabled defaults false; enabling requires Operator:Enabled and Execution:Mode=Inspect and suppresses startup execution. Existing action/live opt-ins still apply. No live actions are authorized by this implementation.

2026-09-08 follow-up: user deploys to an isolated local server and explicitly requires no authentication. Remove the operator-only loopback client/Host restriction, preserve same-origin and CSRF checks, support LAN IP/hostname access, and leave listener/firewall configuration and legacy `/operation/stop` unchanged. Acceptance covers panel assets/snapshots and manual/schedule POSTs from a LAN client, missing-token/cross-origin refusal, and disabled-panel 404.

2026-09-08 archive follow-up: resolve the NoFeasibleCandidate terminal dead end by permitting exact-byte archival of that known failure when all shared journal/identity invariants pass. Project CanArchive using the same guard, explain unavailable archive actions, and reject finished-run restarts with RunFinished. Preserve unknown/pending/other failed outcomes and existing successful archival rules.
