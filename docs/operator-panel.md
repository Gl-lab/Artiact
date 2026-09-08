# Local operator panel

Set `Operator:Enabled=true` to expose `/operator` and `/operator/snapshot` on the existing host. The default is disabled. Use a loopback URL; both the remote address and Host header must be loopback/localhost. The page uses embedded assets with no external dependencies, no-store responses and a restrictive content policy. It does not grant game-action permission or change the configured execution mode.

The panel reads `Execution:RunDirectory` for the configured API origin/character without acquiring the execution lease and without contacting the API. One shared reader refreshes at most once every two seconds; the page polls every three seconds. Atomic replacement can continue while a reader holds the old file. Each checkpoint read is capped at 8 MiB; up to 20 recent archived summaries are displayed. Missing or corrupt storage is distinct from an idle executor. Failed reads remove the previous successful projection; loss of the HTTP connection labels the previously displayed facts stale.

Worker activity comes from the local execution lifetime, not `/health/live`. A nonterminal checkpoint with no worker shows AwaitingRecovery. Confirmed actions come only from Verified journal entries; unresolved commands and incomplete timing remain explicit. Decisions, attempts, time including downtime, charged resources and milestone history come from durable facts. Old checkpoints lacking an observation timestamp show unknown freshness; new character reads/action responses persist optional ObservedAt. No migration or invented timestamps are applied.

The response projects known character fields, run ID, limits and lifecycle facts. Raw identity, policy configuration, catalogs, exception text and credentials are not exposed. The panel is for one local trusted OS user, not remote hosting or multi-user authorization. R16's historical intermittent failure remains open. R17 adds observation only; control is a separate R18 change.

Verification is recorded in the [R17 change](../openspec/changes/operator-observation/verification.md). Browser preview uses synthetic facts; it is not live acceptance. Do not start the main application as a compilation test.
