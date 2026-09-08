# Acceptance

- No active checkpoint, active executor, completed/stopped, Blocked, UnknownOutcome, AwaitingRecovery and unavailable storage are distinguishable.
- Polling never dispatches a game action or acquires ownership. Two clients share a bounded cache; the UI polls every three seconds.
- Budgets and observations come from persisted facts; absent/old/future timestamps are labelled unavailable/stale. Remaining counts cannot be negative.
- Raw credentials, catalog payloads, arbitrary identity fields and exception messages are absent from JSON and HTML.
- Active atomic replacement remains possible while reading; malformed/oversize files produce an unavailable view.
- The local page renders status, budgets, character, explanation, history and result; remote addresses or hostile Host values cannot access it.
- Focused service/HTTP tests, full solution test gate, visual inspection, documentation and self-review precede commit/push.
