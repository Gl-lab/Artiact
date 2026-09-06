# ADR 0001: Bounded combat viability and recovery

Date: 2026-09-06. Status: research decision for Epic 6 design; not production rollout approval.

## Context and evidence

Mining provides an incremental orchestration seam, but existing looting uses level-only combat eligibility. Current fight/equipment/map contracts diverge from the inspected API. See [contract matrix](../research/combat-equipment/contract-matrix.md), [mechanics and uncertainties](../research/combat-equipment/mechanics.md), [alternatives](../research/combat-equipment/comparison.md), [experiment protocol](../research/combat-equipment/experiments.md) and [verification evidence](../../openspec/changes/research-combat-equipment-progression/execution-evidence.md).

## Decision

**Go for a separately specified deterministic Epic 6 HTTP slice; no-go for real-character combat execution at this stage.** Use the conservative local survival bound as the candidate viability model. Optional official simulation and observed calibration may challenge its assumptions, but neither is a required dependency or a substitute for missing state. A full combat emulator is not justified: the proof needs a small supported arithmetic subset; orchestration can use independent scripted responses.

Supported research scope: one character, normal effect-free opponent, one active element, explicit normalized stats and one pre-owned weapon. The exact numeric domains, 50-exchange conservative cap, half-up rounding and loss calculation are recorded in mechanics.md and Predictor.cs. Safe requires at least 1 HP after maximum modeled incoming damage with no favorable outgoing crit/order assumptions. This is a zero-modeled-death criterion within that subset, not a claim of zero live risk. Outside the subset return Unknown. Higher item level alone cannot authorize a swap or fight.

Use a new application-local `CombatLevelGoal` with a required positive target for Epic 6. Do not activate the existing nullable LevelUpGoal implicitly. Leave that legacy contract unsupported until an explicit atomic migration is needed. Combat XP/level and all action state come from authoritative responses. Equip/recovery are finite prerequisites, not autonomous objectives.

## Transition and recovery policy

One simulated mutating command per decision. Default research limits: 20 decisions including terminal reporting, 4 fights, 2 rests, 3 consecutive commands without fight XP/level increase. These are fixture settings, not new application configuration defaults. Charge before dispatch; only a successful fight's XP/level increase resets no-progress. A longer gear chain needs an explicitly larger limit. Rest, movement, equip and failures never refund total budget.

Decision precedence: sticky terminal; cancellation; target/limit validity; structural state validity; target completion; inventory pressure; no-progress; decision budget; access; supported current stats; equipment prerequisites or full-health feasibility; per-action fight/rest budget. Below max HP, rest before a fight. Rest must increase valid HP; repeated partial recovery is still bounded. Keep returned state before any following decision. Defeat, rejected action, invalid postcondition or ambiguous outcome terminates. Defeat retains returned location/HP and does not trigger revenge.

Emit stable Selected/Completed/Blocked reasons and factual snapshots/counters as demonstrated by Decision records. An ambiguous dispatched action has an unknown outcome, never an automatic retry. Future HTTP integration must enforce this below the application invocation boundary. The current production retry loop does not satisfy it.

Restart continuity is intentionally absent from the prototype. A new experiment is a new explicit run with new counters. Future live execution must reconcile character/action state before restarting after ambiguity and define any durable duplicate-prevention policy; process restart is not an implicit retry authorization.

## Consequences and exit boundary

Epic 6 starts with the named synthetic target-2 fixture and minimum subset in [the handoff](../research/combat-equipment/epic-6-handoff.md). It must address DTO response identity, slot/container migration, current map access/content, no-blind-retry transport and complete independent response fixtures before claiming real-client acceptance. Loot/craft extension additionally needs reciprocal drop ranking and shared dependency accounting review. No live route or best monster is selected from stale caches.

Rest formula conflict does not prevent response-driven progression; it prevents a justified time-optimal ranking. Missing complete mechanics and matched external calibration prevent live-safety claims. No operator input is needed to finish Epic 5 or propose the deterministic slice. A future expansion to probabilistic live risk, paid simulation dependence, effects, consumables or recovery after defeat needs an explicit policy decision and evidence. Research is complete with these downstream blockers recorded, not with those blockers silently solved.
