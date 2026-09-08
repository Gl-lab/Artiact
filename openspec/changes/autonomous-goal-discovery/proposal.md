# R11: Autonomous goal discovery

## Why
The full-path selector can only rank manually valued goals. An autonomous Inspect profile must discover a bounded set of useful gathering milestones from observed state without Skills/Target/Value.

## Changes
Add opt-in `Portfolio:AutonomousGoals` and a versioned discovery policy. Keep manual policy serialization unchanged when disabled. Discover supported gathering opportunities, attach auditable utility evidence, and reuse resource alternatives and full-path estimates. Fetch items for opportunity analysis even without item goals. Reject action modes until R12 provides durable goal transitions.

## Non-goals
Execution, checkpoint migration, healing production, combat discovery and live actions belong to later epics. Catalog presence alone does not prove equipment or consumable utility.

## Acceptance
Empty manual goals are accepted only in autonomous mode. Inspect dispatches no actions. Catalog order cannot change the selected goal. Different states select different skills. Unsupported and unaffordable paths retain rejection explanations. Manual behavior remains compatible. See specs and the comparison protocol for independent expectations.
