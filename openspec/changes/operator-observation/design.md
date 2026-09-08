# Design

Use a static HTML/CSS/JavaScript page embedded with the application and a deliberately projected JSON response. Enable using Operator:Enabled (default false). Local-address and Host checks apply to all operator endpoints. No raw checkpoint identity, catalogs, credentials or arbitrary JSON goes to the browser. Render dynamic values with textContent.

Read atomic checkpoint snapshots without acquiring or modifying the execution lease. Open readers with delete sharing so observing a run does not block atomic replacement. Limit file size, history count (20), and actual disk refresh to one per two seconds across clients. Distinguish missing run from unavailable/corrupt storage; never retain stale success after a read failure. History comes from archived checkpoint facts. Show unknown/missing timestamp explicitly; new observations gain an optional persisted timestamp. Remaining wall time includes downtime; returned cooldown is separate from the action budget.

Worker activity is explicit in OperationState, with RunAsync entry/exit in a finally block. A durable nonterminal run with no local worker is AwaitingRecovery, never Running. Only confirmed journal records qualify as the last action. Reconciliation and missing wait facts imply incomplete timing coverage.
