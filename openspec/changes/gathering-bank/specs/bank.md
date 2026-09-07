# Behavior

- Bank support is opt-in and adds required read/action contract probes. Incomplete metadata/pages fail closed.
- Deposit only allowlisted stock above retained quantity. Never consume protected codes. Require sufficient bank slots and a supported current/bank map.
- Inventory-pressure remediation is attached to an unfinished skill goal, not an independent competing goal after completion.
- Move preserves bank and inventory. Deposit preserves every unrelated character field and exact inventory+bank quantities; response items must equal request. Invalid/lost response stops or reconciles read-only.
- Deterministic scenario: capacity 3, one protected item, six ore gathers to mining level 4; two deposits of two ore. Expected 13 actions, 71 virtual seconds, bank ore 4, inventory ore 2 and protected item 1.
