# Observable requirements

- An unexpected execution exception produces ExecutionFailed, never a false file diagnosis; diagnostics contain no injected secret message. Existing explicitly handled invalid-policy exceptions retain their original reason.
- Read, restore validation and write failures identify their operation while retaining Blocked and existing counters.
- A directory at the checkpoint filename is unavailable state, not a new run. A missing parent directory is an error. An absent file in an existing directory is a new run.
- Failed intent persistence sends zero POSTs. Failed response persistence retains the durable intent; reopening reconciles without dispatch. Budgets survive restart.
- Damaged JSON, exclusive sharing conflicts and invalid checkpoints stop safely. Released ownership permits subsequent valid opens.
- Original transient acceptance requires captured causal evidence, not passing repetitions.
