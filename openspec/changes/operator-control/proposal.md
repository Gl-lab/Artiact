# R18: Explicit local run control

Base `ebe20d6`. Add preparation/Inspect, explicit start, same-run recovery, stop and guarded archive to the local panel. Reuse StagedExecution and FileRunCheckpointStore lifecycle rules used by the CLI. Do not implement a second planner or action transport. R16 historical diagnosis remains open by explicit user decision.

Scope: one local noncombat server-configured portfolio, editable finite limits bounded by server configuration, explicit RunId. Operator:ControlsEnabled defaults false; enabling requires Operator:Enabled and Execution:Mode=Inspect and suppresses startup execution. Existing action/live opt-ins still apply. No live actions are authorized by this implementation.
