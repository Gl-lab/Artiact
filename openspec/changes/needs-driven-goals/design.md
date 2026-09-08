# R25.1 design contract

## Parents and modes

Needs mode is explicit and mutually exclusive with general-development discovery and manual milestone portfolios. Existing configurations retain their meaning and identity when the new option is absent. Needs mode cannot be passed to R20: registration and direct schedule execution reject it before any reservation or checkpoint.

Parent evidence contains stable ID, source (Order/ObservedHp), revision, observed deficit, absolute satisfaction criterion, priority and cancellation condition. Order goal means own N units above protected floors, counting permitted bank availability exactly once; it never means craft another N on restart. HP parents are generated from current deficit with Recovery permission and expire when HP is restored. They are not replayed from history. Child evidence contains ParentId, causal prerequisite, full destination and bounded slice destination. Only the parent owns utility; slices keep cost/progress evidence and receive no additive reward.

## Orders survive runs

Use an explicit order store separate from run history, under an `orders` subdirectory keyed by canonical character identity (never root-level JSON, which the existing run store treats as foreign checkpoints). Schema version, stable operator-chosen ID, code, absolute quantity, priority, revision and Active/Cancelled/Completed status are durable. Finite maximum 128 orders; invalid, duplicate, unsupported or corrupt entries fail closed. No automatic recreation of completed/cancelled IDs.

Order mutations take a separate exclusive file lease, validate expected revision, write a same-directory temporary file, Flush(true), then atomically replace. Read complete old/new files only. No secrets or raw catalogs. Cancellation/completion tombstones remain after run archive. Read-only Inspect does not mark an order completed; bounded execution records observed completion idempotently. If process loss occurs between checkpoint and order write, a fresh observation and revision compare safely repeat only the local status update. Never replay an action to repair an order-store write.

An order update is not an execution permission. The next tick observes current revisions; pending game commands reconcile using the original saved baseline before parent replacement. A new RunId reads active orders and fresh stocks. It receives only its own explicit budgets; unfinished same-ID restart restores all counters, charges and pending state. R20 series counters are never refunded.

## Full chain and finite slices

Prove a supported acyclic chain with available recipes, permitted transfers/actions, reachable routes and adequate working capacity before choosing a slice. Depth <=16 and <=32 alternatives per parent, bounded steps <=256. Reserve a single shared stock ledger, protected floors and batch surplus. Reject unknown effects/professions, cycles and unreachable or forbidden later steps even if an earlier step would fit.

Compare parent priority first, then useful parent result per full estimated cost (actions, time including preparation/banking and material charges), then stable ordinal IDs. Distinguish observations from estimates. Existing ready stock and allowed withdrawals are alternatives; available bank stock never grants transfer permission. HP may choose allowed rest. Training and ingredients earn no extra utility.

Only when the full chain exceeds this run's budget may a smaller slice be selected: minimum prerequisite level, bounded ingredient batch or intermediate product that persists and has a proven parent use. Movement alone is not a slice. Keep full-path cost separate from slice cost. After slice completion, observe and replan within remaining budget; insufficient next slice yields Stopped/budget with parent unsatisfied. No automatic subsequent run.

## No-progress semantics

Follow actual StrategySession.Tick ordering: threshold is tested before observing/evaluating; dispatch increments NoProgress; only a verified Productive command resets it. Skill preparation retains the parent's Productive semantics. For k nonproductive commands followed by a productive command, current NoProgress + k must be strictly below the limit so the productive tick can begin. A purely nonproductive slice needs current NoProgress + k strictly below the limit if completion requires another evaluation tick. Decisions must likewise include that final evaluation. Record required and configured limits in refusal evidence; do not increase limits automatically. Switching slice/parent/resource or restarting never resets the run counter. Unexpected preflight replans can still exhaust a valid estimate.

## Lifecycle and compatibility

| Condition | Result | Durable effect |
|---|---|---|
| Inspect with no active supported needs | Stopped / NoActiveSupportedNeeds | no receipt, terminal latch or checkpoint; later Inspect reevaluates |
| Executing session exhausts needs | Stopped / NoActiveSupportedNeeds | persist terminal; reopening performs no game actions |
| Need exists but all chains unsupported | Blocked / NoFeasibleCandidate | typed reasons, no claim that needs are satisfied |
| Next valid slice exceeds budget | Stopped / AutonomousBudgetExhausted | parent persists unsatisfied |
| Pending or unknown action | existing reconciliation/UnknownOutcome | do not switch parent or relax archive guards |

Archival extends the existing verified predicate for this named terminal only, requiring no pending command/candidate, valid schema/parent data, complete consistent counters and verified/reconciled journal, initial/latest facts and finished timestamp. Corrupt or unknown checkpoints remain unarchivable. Manual normal no-needs outcome must not become a technical worker error. Order store survives archival.

Needs schema/algorithm and policy identity are versioned separately on implementation. Do not reinterpret old gathering goals as needs. R22 uses optional intermediate-intent evidence and a new gathering discovery version; old active gathering checkpoints fail closed for execution on algorithm mismatch, but remain readable for observation and eligible historical archival with their original schema validation. Never migrate or drop pending intent automatically.

## Implementation refinement after R22–R24

See implementation-plan.md and ../../../../docs/needs-driven-goals.md. Storage origin isolation is an explicit per-origin Directory; canonical character hash is the file key. Version needs-v1 is part of policy identity. Each order has at most two route alternatives (bank-permitted/local); HP retains up to 32 foods plus rest. Recipe depth for orders is 16 and simulation is at most 256 commands. Priority precedes inverse full estimated time, with material/stock permissions as feasibility constraints rather than a invented economic score. Guaranteed min yields advance stock while maximum yields gate capacity. Official XP formulas reuse observed future max_xp as a labelled estimate, never execution evidence.
