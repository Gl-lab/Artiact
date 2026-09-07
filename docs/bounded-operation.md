# Durable bounded execution

R10 `run-result` adds performance coverage, known XP/unknown transitions, consumed inputs and dispatched candidate switches. Action facts are persisted before cooldown and completed wait time afterward; unknown/reconciled outcomes create no timing samples. See [full-path selection](full-path-selection.md) for the distinction between returned cooldown, measured processing/wait and total elapsed time.

R8a adds optional resource charges, refill state and selected pending-candidate identity to the journal; see [consumables](consumables.md). `Execution:MaxNoProgress` defaults to 10 and may be explicitly raised for longer preparation chains, up to MaxDecisions. Existing default identities/limits are preserved.

R2 adds explicit `Execution:Mode=Bounded`. Configure the same API/Portfolio as Inspect and set `AllowActions=true`, stable `RunId`, absolute `RunDirectory`, positive `MaxActions`, `MaxDecisions` (at least 10) and `MaxSeconds` (1–86400). Defaults for budgets are 100/200/3600; directory and ID have no defaults. Live origin additionally requires the existing live opt-in and separate rollout evidence. Default host mode remains Inspect.

Use one shared local directory for all cooperating bounded executors for an origin/character. `.artiact-runs/` is git-ignored; other directories must also remain outside version control. Checkpoints contain character/catalog state, never credentials. The exclusive filesystem lease is held through execution/cooldown; a competing owner fails before observing or acting. This is not a distributed lock and cannot stop external game clients or standalone OneShot/Legacy executions. Do not run those concurrently.

Character ownership keys ignore case. Use a directory dedicated to one origin/character. An unexpected JSON checkpoint (including an older case-sensitive filename) blocks startup for operator migration; preserve unresolved intents and budgets when migrating.

The versioned checkpoint stores run/policy/limits identity, start time, decision/action/no-progress counters, consumed commands, command journal, pending baseline, last verified observation and terminal decision. Command delegates are reconstructed solely to check pending postconditions; they are not a durable representation or blindly resent. Each intent is flushed before POST; verified results are flushed before cooldown. Atomic file replacement provides process-crash consistency on supported local filesystems, not a power-loss/storage-device guarantee.

On restart, pending intent is reconciled by reading. Even a crash before POST can remain UnknownOutcome: unchanged state is insufficient proof to retry. Observing the expected postcondition does not attribute the action to this executor. Budgets include downtime and cannot be reset by changing configuration or run ID. A terminal checkpoint remains stopped. Use the offline lifecycle commands below to review and archive a verified completion; unresolved, cancelled and blocked runs cannot be cleared by this path.

## Review and start another run

Stop the host first so it releases its character lease. These commands exit before host construction, configuration/secrets loading, clients or telemetry initialization:

```text
dotnet Artiact/bin/Debug/net9.0/Artiact.dll run-result <absolute-run-directory> <origin/character>
dotnet Artiact/bin/Debug/net9.0/Artiact.dll run-archive <absolute-run-directory> <origin/character> <IdentityDigest-from-result>
```

The ownership identity must match the Bounded origin authority plus `/` and character (for example `http://localhost:5001/researcher`). `run-result` prints JSON containing policy/run identity, initial/latest/verified character and bank facts, level/XP and stock changes, attempts, journal, confirmed cooldown, terminal time and intervention requirement. XP deltas are changes in the reported level-local XP counter, not lifetime XP gained. Elapsed time includes downtime. Lost replies contribute no invented cooldown. Missing facts in older checkpoints remain null.

`run-archive` requires the exact case-sensitive SHA-256 identity digest, Completed/TargetsReached, a consistent successful journal and available final facts. It atomically moves the original checkpoint bytes into `history/<character-key>/<identity-digest>.json`. Failures leave the active checkpoint in place. Exit codes are 0 success, 1 refused/unavailable, 2 invalid command arguments. History contains private operational state and must remain outside Git.

After archival, explicitly configure a new `Execution:RunId` and the next goal/budgets, then start Bounded normally. Archived identities cannot be reused even if the previous command or process was interrupted after archival. There is no automatic next run. Resume an unfinished run by retaining its original configuration and identity; terminal states remain terminal. Legacy/OneShot do not gain a durable lifecycle from these commands.

`GET /operation` exposes run ID, goal, last command, decision budgets and intervention flag. `POST /operation/stop` requests cooperative cancellation and schedules no game action. Host shutdown also cancels. These operational routes follow the host's existing network exposure; deploy behind appropriate local access controls. Cancellation preserves returned action state; an in-flight HTTP POST may finish before stopping. Cooldown is awaited rather than actively polled.

Socket-free evidence: [R2 execution evidence](../openspec/changes/durable-bounded-run/execution-evidence.md). [Live session acceptance](../openspec/changes/bounded-live-gathering/execution-evidence.md) verified two real gathers, reconstructed sessions and persistent cancellation for gllab. The separate `RealApiBounded` category requires `ARTIACT_BOUNDED_ROLLOUT=gllab:mining2:max4`; its fixed ignored checkpoint prevents automatic reruns. This was not process-kill recovery or proof of reaching mining 2. Container persistence and telemetry are verified separately.
