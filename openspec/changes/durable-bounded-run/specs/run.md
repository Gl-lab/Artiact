# Requirements

- Storage failure before intent persistence means zero dispatch. Failure after dispatch means no further dispatch in that executor.
- Startup with pending intent reads current state and checks the saved postcondition. Unchanged/ambiguous state remains terminal UnknownOutcome, even if the process died before POST. Observed postcondition is not attribution.
- Decisions, attempts, no-progress and deadline survive restart. Attempt reservation precedes POST; unknown results never refund budget. Reconciliation is allowed at an exhausted budget and cannot send POST.
- Exclusive character ownership prevents overlapping bounded executors sharing the same store directory. Non-cooperating external clients remain a race; preflight and outcome checks retain fail-closed behavior.
- Bounded requires explicit action opt-in. Cancel and time/action/decision limits terminate predictably. Cooldown uses an awaited delay, not active polling.
- Status exposes run ID, current candidate/command, counters and terminal/intervention reason, without raw snapshots.
