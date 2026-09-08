# Design

The dedicated read-only verifier acquires one observation through its existing destination/GET allowlist. A frozen observer then supplies that observation to fresh Inspect-only StrategySessions, with production AutonomousGoalDiscovery and measurement policy. No matrix row performs network reads or dispatches. The original preflight category remains a historical baseline.

Preregistered sensitivity cases: actions/seconds 2/120, 12/600, 13/120, 13/134, 16/180, 16/600, each with 20 decisions and three consecutive no-progress decisions. Thirteen is the previous snapshot's estimated minimum (12 gathers + one move); sixteen allows three extra gathers. Reassess if fresh data invalidates that estimate rather than searching unboundedly for a passing case.

Report acquisition elapsed time and GET count separately from configured action estimates. Budget elapsed time for up to two full observations per action, configured cooldown with the existing unknown-cost multiplier, and explicit headroom. These are engineering allowances, not measured future latency guarantees. Preserve inventory feasibility and require concrete approval and command allowlist before execution.
