# Design and compatibility

Discovery v2 adds optional original-target/reason fields to GoalDiscoveryEvidence. Candidate IDs for intermediate alternatives use a stable suffix to retain both rejected original and smaller alternative in the decision. Active intermediate evidence restores the same target/identity; no recursive fallback. Restrict eligibility to routes whose original refusal is inventory/budget, preserving all other route guards. Bound search to one additional target per skill.

Policy DiscoveryVersion changes, so existing active policies cannot silently resume. Recognize v1 autonomous data for read-only results and existing archive guards; execution restore explicitly requires the current algorithm. Unknown versions still fail closed. Pending outcomes are not replayed/migrated across versions; compatible current-version pending state still reconciles before selection.

Test inventory exact boundary, maximum simultaneous drops, budget refusal, nearest-goal deduplication, invalid access, active intermediate restoration and utility. Use existing execution tests for completion/restart and add intermediate-specific persistence coverage. No limit refund, new bank permission or live action.
