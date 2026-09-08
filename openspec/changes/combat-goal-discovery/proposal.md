# R14: Combat goal discovery

An autonomous profile currently cannot discover combat levels/opponents/gear without manual R9 stages. Add an optional, default-disabled CombatDiscovery permission profile and reuse the supported R9 predictor and R10 full-path estimator. No manual Monster, Equipment, stage, target or value is required.

Affected projects: application Operation/Strategy, application tests and deterministic mock/tests; documentation and ADR. No shared DTO additions or live actions. The R9 conservative subset and live no-go remain binding.

Acceptance: finite catalog-derived next-level candidates, state-dependent current/weapon/shield routes, manufacturing and equipping a useful replacement, full-path competition with gathering/recovery, explicit denied actions, bounded shared resource charges, every-tick restart and unknown-result reconciliation. Unsupported/cyclic/unavailable routes reject without loops. Run the entire accumulated comparison matrix before commit/push and R15.
