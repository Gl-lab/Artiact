# Autonomous Inspect requirements

## Requirement: Explicit mode compatibility
The application SHALL accept an empty manual portfolio when AutonomousGoals is enabled, reject mixed manual goals, and reject non-Inspect autonomous execution in R11. Disabled mode SHALL retain the previous identity and validation.

### Scenario: Empty autonomous portfolio
Given configuration containing only AutonomousGoals=true, policy binding succeeds; the factory can Inspect discovered candidates without any action dispatch.

## Requirement: Bounded deterministic discovery
Discovery SHALL use validated observed state and catalogs, keep at most four gathering milestones, respect cap 50, and rank feasible routes by versioned utility divided by full-path cost. Unsupported observations, access, inventory and over-budget estimates SHALL be explained as rejections. No combat field SHALL be required for gathering.

### Scenario: Order and reachability
Reordering resource/item/map records leaves the selected goal and utility unchanged. A resource on an unsupported map cannot earn unlock utility. Missing training paths cannot displace executable goals.

## Requirement: Explanation
Each generated candidate SHALL carry version, skill target, utility components, unlock provenance, dependent recipes, cost assumptions and reevaluation trigger. Recipe references alone SHALL NOT earn production utility. Exhausting supported opportunities SHALL NOT assert that the character is fully developed.
