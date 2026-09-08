# Checkpoint diagnostics

R16 adds optional `Failure` facts to a failed StrategyDecision. `Operation` distinguishes Read, Restore, Write and Execution; ExceptionType and numeric ErrorCode identify the failure without publishing exception messages, paths, identity or payload. ReconciliationRequired is conservative: a write failure after an attempt must be reviewed against the persisted intent. A failed session cannot continue writing on another tick.

Storage failures retain `Blocked / CheckpointUnavailableOrInvalid`. Unexpected execution exceptions use `Blocked / ExecutionFailed`. Existing explicit observation, policy, cooldown, unknown-outcome and budget reasons remain unchanged. Neither kind is a successful completion. A diagnostic is available in the returned decision and existing operation status/logging; if persistence itself failed, it is not guaranteed to be in the checkpoint.

File loading returns no run only for FileNotFoundException. Invalid JSON, directory paths, inaccessible files and missing parent directories fail closed. Ownership is released when constructor validation fails. There are no new POST retries or checkpoint replacement retries.

The directory-path and error-classification defects have deterministic regression coverage. Their relationship to historical R12/R13/R15 intermittent failures is **unproven**; R16 remains partial until the original cause is captured. See [change evidence](../openspec/changes/managed-reliability/verification.md). Existing process-kill acceptance and pending reconciliation remain authoritative; no filesystem power-loss or live acceptance is claimed.
