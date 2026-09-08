# Read-only budget assessment

- All rows use the same character/catalog observation and production decision logic.
- Inspect returns zero action attempts, even when a candidate is Selected.
- Changing budgets resets remaining-action/time context for each row; baseline context must not leak.
- Acquisition remains guarded by ARTIACT_REAL_API_READONLY=1, pinned origin and the existing read-only transport. Raw authenticated payloads are not emitted.
- Proposed ceilings are documented with arithmetic and uncertainty; no game-action opt-in or runtime default is changed.
