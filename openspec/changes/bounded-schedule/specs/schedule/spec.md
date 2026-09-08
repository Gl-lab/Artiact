# Observable schedule behavior

- Disabled/default configuration registers no schedule worker and dispatches no scheduled work.
- Finite configured interval, expiry, run count and total action/decision/time reservations bound every series across fresh RunIds and restart.
- Each run checks prior checkpoint/character ownership, fresh Inspect and supported policy before dispatch; production execution rechecks ownership and freshness.
- Persist Running and full reservations before invoking execution. Running after restart stops for intervention; counters never reset.
- Archive only verified safe terminal runs using existing lifecycle rules. Unknown/cancelled/corrupt/foreign state blocks continuation.
- Missed intervals cause at most one run; no backlog or concurrent run.
- Stop/host shutdown cancels active shared execution, retains result, and prevents subsequent scheduled work.
- Significant transitions create stable local notification IDs; unchanged polls/ticks do not duplicate notifications. Retain100 newest events, with monotonic sequence across restart.
