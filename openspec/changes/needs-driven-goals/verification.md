# R25.1 design review — 2026-09-08

Reviewed base `4a3221e`, documentation-only specification diff in this directory. Self-review against roadmap R25.1/R25.4 and production StrategySession.Tick, AutonomousRunState, FileRunCheckpointStore plus production/preparation/recovery docs.

Explicit decisions: separate orders subdirectory avoids foreign-checkpoint protection; revision/tombstone writes are independent of RunId; strict no-progress boundary includes the next evaluation tick; schedule mode is excluded before reservation; pending commands retain original context before replan. Runtime tests are not applicable to this design-only slice. R22–R25 runtime and live acceptance are not claimed.
