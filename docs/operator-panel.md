# Local operator panel

Set `Operator:Enabled=true` to expose `/operator` and `/operator/snapshot` on the existing host. The default is disabled. Use a loopback URL; both the remote address and Host header must be loopback/localhost. The page uses embedded assets with no external dependencies, no-store responses and a restrictive content policy. It does not grant game-action permission or change the configured execution mode.

The panel reads `Execution:RunDirectory` for the configured API origin/character without acquiring the execution lease and without contacting the API. One shared reader refreshes at most once every two seconds; the page polls every three seconds. Atomic replacement can continue while a reader holds the old file. Each checkpoint read is capped at 8 MiB; up to 20 recent archived summaries are displayed. Missing or corrupt storage is distinct from an idle executor. Failed reads remove the previous successful projection; loss of the HTTP connection labels the previously displayed facts stale.

Worker activity comes from the local execution lifetime, not `/health/live`. A nonterminal checkpoint with no worker shows AwaitingRecovery. Confirmed actions come only from Verified journal entries; unresolved commands and incomplete timing remain explicit. Decisions, attempts, time including downtime, charged resources and milestone history come from durable facts. Old checkpoints lacking an observation timestamp show unknown freshness; new character reads/action responses persist optional ObservedAt. No migration or invented timestamps are applied.

The response projects known character fields, run ID, limits and lifecycle facts. Raw identity, catalogs, exception text and credentials are not exposed. The panel is for one local trusted OS user, not remote hosting or multi-user authorization. R16's historical intermittent failure remains open.

## Explicit control (R18)

Additionally set `Operator:ControlsEnabled=true` with `Execution:Mode=Inspect`. This combination suppresses the startup worker: opening the host does not automatically inspect or act. Other combinations fail registration. Configure the noncombat Portfolio and API credentials on the server, and a dedicated absolute Execution:RunDirectory. The page prepares a RunId (1–80 letters/digits/underscore/hyphen, first character alphanumeric) and finite bounds at or below Execution's configured ceilings. The current minimum MaxDecisions is 10. Combat profiles are refused.

`GET /operator/controls` supplies a projected profile, permissions, stocks, ceilings and a per-process CSRF header token. `POST /operator/inspect` accepts RunId, MaxActions, MaxSeconds, MaxDecisions and MaxNoProgress. A Selected result returns an opaque receipt bound to the exact policy, origin/character, limits and permission configuration. `POST /operator/start` accepts `{ "Receipt": "..." }`. Receipts expire after FreshnessSeconds; configuration changes require another Inspect. The receipt does not bypass AllowActions or LiveActionsApproved. HTTP 409 carries a named refusal; HTTP 403 denies access.

All operator POSTs require `X-Artiact-Control`, a local address/Host and same-origin browser metadata. No cross-origin policy is enabled. The existing `/operation/stop` now also requires local-address/Host and same-origin browser metadata; local non-browser CLI POSTs remain supported. A stop sent through a container bridge/remote interface is refused; invoke it from the host/container loopback boundary.

The coordinator owns one background execution; browser refresh/closure does not cancel it. A repeated receipt returns AlreadyAccepted, without another execution. Busy executors refuse other starts/Inspect. Start rechecks the local lease and durable identity; StagedExecution independently acquires the lease and preserves its own preflight, intent and retry rules. The same StagedExecution and archive guards serve CLI and panel.

“Подготовить восстановление” copies the durable RunId and all limits into the form for a new Inspect; it does not create a new run or refund budgets. Host restart discards receipts and preserves checkpoints. “Остановить” requests cancellation; a dispatched successful response is persisted before following work stops. “Архивировать итог” passes the current identity digest to the shared lifecycle guard. Unknown, Blocked and Cancelled runs remain protected; the new-ID button does not clear an existing run. Host shutdown requests cancellation and awaits the owned task.

The existing [live protocol](autonomous-live-protocol.md) remains a separate unexecuted gate. Its proposed six-decision ceiling currently conflicts with the existing ten-decision minimum and must be resolved in R19 without silently enlarging the live scope.

Verification is recorded in the [R17 change](../openspec/changes/operator-observation/verification.md). Browser preview uses synthetic facts; it is not live acceptance. Do not start the main application as a compilation test.
