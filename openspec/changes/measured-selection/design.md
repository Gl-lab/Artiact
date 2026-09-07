# Design

Extend strategy enumeration with default single-candidate behavior and opt-in multi-resource candidates. Filter planning catalogs only while retaining the full-world command/reply fingerprint. Explicit monster alternatives each use the combat safety strategy; existing item goal list supplies item alternatives.

Store per-candidate/action-kind sample count, total returned seconds and productive progress count. Candidate ranking uses the observed mean for that component, explicit fallback multiplier for unknown components and existing travel/recovery estimates. Expose source/sample metadata. No measurement is inferred from lost replies or wall-clock HTTP latency. Persist samples/incumbent in checkpoint.

Costs remain bounded estimates: recipe candidates expose the remaining deterministic recipe/gather/withdraw/movement work; unknown loot loops retain explicit conservative estimates and global budgets. Switching requires a strict score advantage beyond the configured ratio. Completion/rejection removes the incumbent naturally.
