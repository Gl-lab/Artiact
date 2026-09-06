## Context

The current path is `ArtiactBackgroundService -> IActionService.ExecuteCycleAsync -> IGoalService.GetGoal -> GoalDecomposer -> StepBuilder`. `GoalService.GetGoal` always creates `GatheringGoal(20)`. `GoalDecomposer` notices fewer than ten free inventory units only after selection and can build an empty crafting prerequisite. Separately, `StepBuilder` repeats gathering within one selected cycle while two units remain free and ignores `GatheringGoal.TargetLevel`. A selector-only guard would therefore be false safety: one selected cycle could cross both the inventory and level boundaries without reevaluation.

This change introduces the smallest complete decision boundary for one configured mining milestone. It fails closed on inventory pressure and makes the existing live gather predicate enforce the same target and ten-unit reserve after every authoritative action response. The later mining-progression change will choose resources/movement and may add a proven inventory remediation path.

## Goals / Non-Goals

**Goals:**

- Produce the same valid typed decision for the same character values and target setting.
- Distinguish selected work, completed milestone, and blocked progress before goal decomposition.
- Ensure one selected gathering step cannot start another gather after reaching the target or crossing below ten free units.
- Prevent decomposition, step construction, client actions, and rapid worker repetition for terminal decisions.
- Expose stable reason codes and typed facts without prose parsing, timestamps, randomness, external I/O, or sensitive payloads.

**Non-Goals:**

- No generic candidate list, weighted score, optimizer, strategy interface, or plan state machine.
- No resource/map-selection change and no claim that one cycle autonomously reaches the target.
- No crafting, deletion, recycle, bank, or other inventory-pressure remediation.
- No MockService route/scenario change and no production/live API verification.
- No durable decision history, database, broker, dashboard, or new telemetry dependency.

## Decisions

### 1. Make invalid decision shapes unconstructable

`IGoalService.Evaluate(Character?)` returns an immutable `GoalDecision`. `GoalDecisionStatus` contains exactly `Selected`, `Completed`, and `Blocked`; `GoalDecisionReason` contains exactly `MiningBelowTarget`, `MiningTargetReached`, `InvalidGoalPolicy`, `InvalidCharacterSnapshot`, `InvalidInventorySnapshot`, and `InventoryPressure`. A stable lowercase snake-case `ReasonCode` is derived exhaustively from the reason enum.

`GoalDecision` has no public constructor or setters and does not retain a mutable `Goal`. It stores nullable `SelectedGoalType` using the existing `GoalType` enum. Named factories validate and create only these combinations:

| Status/reason | Current level | Inventory facts | Selected goal type |
|---|---:|---|---|
| `Selected/MiningBelowTarget` | observed non-negative and below target | non-null, internally consistent, free >= 10 | exactly `GoalType.Gathering` |
| `Completed/MiningTargetReached` | observed non-negative and >= target | always null, even if source inventory is valid | null |
| `Blocked/InvalidGoalPolicy` | always null because policy validation precedes character inspection | null | null |
| `Blocked/InvalidCharacterSnapshot` | null when character is absent; otherwise the observed invalid level | null | null |
| `Blocked/InvalidInventorySnapshot` | observed non-negative below target | null | null |
| `Blocked/InventoryPressure` | observed non-negative below target | non-null consistent capacity/used/free with free < 10 | null |

All decisions also contain the supplied `MiningTargetLevel` and `RequiredFreeInventory=10`. The factories reject invalid enum values, wrong reason/status pairs, a non-Gathering selected type, a selected type on terminal decisions, contradictory inventory arithmetic, and selected facts below the reserve. `ActionService` accepts only factory-created decisions and does not duplicate those invariants. For `Selected`, it creates a fresh mutable `GatheringGoal(decision.MiningTargetLevel)` solely as the execution graph; decomposition cannot mutate the immutable returned decision.

The selector receives the character snapshot directly so it owns no mutable state. Decision data contains no wall-clock value, random ID, generic score, user-facing control-flow prose, or mutable inventory collection.

### 2. Use fixed validation precedence and explicit malformed-data rules

Evaluation precedence is:

1. invalid target (`<= 0`) -> `Blocked/InvalidGoalPolicy`;
2. null character or negative current mining level -> `Blocked/InvalidCharacterSnapshot`;
3. current mining level at least target -> `Completed/MiningTargetReached`, with all inventory facts null and no inventory access;
4. below target: null inventory list, null inventory element, negative capacity, negative quantity, blank item code with positive quantity, checked-sum overflow, or used greater than capacity -> `Blocked/InvalidInventorySnapshot` with all inventory facts null;
5. valid free space below ten -> `Blocked/InventoryPressure` with exact inventory facts;
6. otherwise -> `Selected/MiningBelowTarget` with exact facts and `SelectedGoalType=GoalType.Gathering`.

Completion precedes inventory validation deliberately: reaching the milestone selects no mutation and reports no inventory claim. Missing or malformed inventory never produces inventory facts for Completed; the fields and their telemetry tags are always omitted/null for that status.

Normative examples for target `20`:

| Snapshot | Decision |
|---|---|
| mining `19`, capacity `20`, used `10`, free `10` | `Selected/mining_below_target`, selected type `GoalType.Gathering` |
| mining `19`, capacity `20`, used `11`, free `9` | `Blocked/inventory_pressure`, no goal |
| mining `20` or `21`, any inventory including null/malformed | `Completed/mining_target_reached`, null inventory facts, no goal |
| mining `19`, null list/element, negative quantity, overflow, or used > capacity | `Blocked/invalid_inventory_snapshot`, null inventory facts, no goal |

### 3. Keep configuration narrow and share production registration with tests

Add `GoalSelectionSettings` with only `MiningTargetLevel`. Add one `AddGoalSelection(IServiceCollection, IConfiguration)` extension that binds section `GoalSelection`, validates `MiningTargetLevel > 0`, enables startup validation, and registers `IGoalService`. `Program` calls this extension; safe DI tests call the same extension without constructing the host or registering the worker. Tracked `appsettings.json` sets `MiningTargetLevel` to `20`, preserving the old effective target.

No inventory threshold option is added: ten is existing policy and normative for this MVP. Invalid configuration prevents autonomous startup. Direct `GoalService` construction/evaluation still fails closed with `InvalidGoalPolicy`, making the pure boundary total under tests.

### 4. Migrate the interface without compilation-only RED

The migration remains compiling in two vertical slices:

1. Write tests against the desired decision API. Their initial missing-type compilation failure is setup evidence, not RED. Add the smallest compiling type/interface skeleton with factories and `Evaluate(Character?)` throwing `NotImplementedException`, while retaining `GetGoal(ICharacterService)` temporarily. Rerun until the tests compile and fail on missing behavior; only that behavioral failure is recorded as RED. Implement the pure decision behavior; existing `ActionService` continues compiling through `GetGoal`.
2. Add a compile-only orchestration skeleton: change `IActionService.ExecuteCycleAsync` and all callers/test doubles to `Task<GoalDecision>`, make `ActionService.ExecuteCycleAsync` deliberately throw `NotImplementedException` after its existing pre-cancellation check, and leave `GetGoal` temporarily available. This skeleton does not implement selection or execute an old goal. Add behavioral tests for one `Evaluate` call, the exact returned decision, and status branching; record RED from the deliberate missing orchestration behavior. Then implement the branch through `Evaluate` and remove `GetGoal` after a repository search proves no callers remain.

A compile or mock-setup error is not RED evidence. The compiling skeleton adds no working policy and exists only to expose behavioral RED. The temporary compatibility method exists only between the two GREEN checkpoints and is removed in the same change.

### 5. Enforce target and reserve immediately before every gather

`StepBuilder.BuildMiningSteps` receives the selected `GatheringGoal` and uses the same inventory-validation helper/rule as selection. Blank/null item code is valid only with quantity zero and contributes zero; blank/null code with positive quantity is malformed. A `ConditionalStep` applies one live predicate before the first gather (including after any preceding move response), and the `ActionStep` repeat predicate applies it after each gather response. It returns true only when both are true:

- `MiningLevel >= 0 && MiningLevel < gatheringGoal.TargetLevel`;
- checked valid free inventory under that shared rule is at least `10`.

Before changing the predicate, focused tests reproduce current defects: a move response that already reaches target or invalidates inventory must prevent the first gather; starting a gather at free 10 then receiving free 9 must prevent a repeat; a gather response that reaches target must prevent a repeat; negative mining level or malformed inventory must never authorize a gather. Invalid live facts make the predicate return false rather than throw. The authoritative response remains stored, the selected cycle returns normally, and the worker performs one next cycle: selector evaluation emits Completed or the applicable typed Blocked, after which the worker stops without recovery delay. The path has exactly two decision events and two cycle calls; action count is zero when movement invalidates the authorization, otherwise one gather.

This intentionally changes the existing two-unit repeat threshold and makes `GatheringGoal.TargetLevel` effective for stopping repeated gathers. Resource candidate and movement behavior remain unchanged.

### 6. Let ActionService enforce the decision boundary

Change `IActionService.ExecuteCycleAsync` to return `Task<GoalDecision>`. `ActionService` checks cancellation, reads the current character once, evaluates once, emits the authoritative decision explanation, and branches:

- `Selected`: construct a fresh `GatheringGoal(decision.MiningTargetLevel)`, decompose/build/execute that private graph exactly once, then return the original immutable decision;
- `Completed` or `Blocked`: return without decomposition, building, step execution, or game-client action calls.

Cancellation is checked before character read/selection and remains propagated through selected execution. Existing exception activity status behavior remains. A thrown implementation/infrastructure error, including any impossible factory invariant failure, follows the existing worker exception recovery path; expected Completed/Blocked decisions do not.

### 7. Stop the worker normally on terminal policy outcomes

The worker awaits each cycle decision. `Selected` continues the existing loop. `Completed` and `Blocked` return normally after the one authoritative decision log has already been emitted by `ActionService`; the worker does not emit a second decision record. They do not enter recovery delay and do not reevaluate the unchanged snapshot.

Automatic wake-up for blocked external conditions is deferred until observation refresh/replanning exists. Restart reevaluates server state after initialization.

### 8. Use one authoritative explanation producer

`ActionService` emits exactly one structured information log per evaluated cycle and tags its existing activity with `goal.decision.status`, `goal.decision.reason`, `goal.mining.target_level`, and `goal.mining.current_level` only when observed. It emits inventory tags `goal.inventory.capacity`, `goal.inventory.used`, `goal.inventory.free`, and `goal.inventory.required_free` only when the decision contains valid inventory facts. Completed and invalid-snapshot decisions therefore have no inventory tags; null-character decisions also omit current level.

Values come only from the immutable decision. Inventory contents, account, credentials, authorization data, serialized character, timestamps, and random identifiers are excluded. Tests assert structured values/event cardinality, not formatted prose. No activity listener/exporter remains behaviorally irrelevant.

## Risks / Trade-offs

- [Blocked inventory needs manual intervention] -> Intentional fail-closed behavior; mining progression may add a proven remediation.
- [One selected step can otherwise overrun a decision boundary] -> Recheck target and the same reserve against every authoritative response before repeating.
- [Interface migration could produce compilation-only RED] -> Use the temporary compiling compatibility method and remove it after behavioral migration.
- [Worker exits on a transient blocked snapshot] -> No safe refresh cadence exists yet; restart-and-reevaluate is safer than a hot loop.
- [Decision/log invariants could drift] -> Centralize valid construction and derive telemetry from the immutable decision.
- [DI tests could duplicate Program wiring] -> Program and tests use the same registration extension.

## Migration Plan

1. Record baseline HEAD/status and warnings; preserve unrelated unstaged `.serena/project.yml`.
2. RED->GREEN: pure decision/factory tests and implementation while temporary `GetGoal` keeps callers compiling.
3. RED->GREEN: behaviorally migrate ActionService to `Evaluate`, return decisions, then remove `GetGoal` after all callers move.
4. RED->GREEN: reproduce and fix repeat-after-free-9, repeat-after-target, and malformed-post-action inventory without changing resource/movement selection.
5. RED->GREEN: terminal worker behavior and single-source structured explanation.
6. RED->GREEN: shared DI registration and actual option validation without host/worker startup.
7. Update affected docs; update `AGENTS.md` only if a durable recurring instruction changes.
8. Run focused tests, full build/solution tests, strict OpenSpec validation, and scans. `RealApiOffline` is not rerun unless this change unexpectedly touches its project or shared API DTOs; live tests/main host remain unverified.
9. Freeze the exact staged diff and obtain independent fail-closed final review.
10. Commit/push require separate explicit authorization. Rollback is a normal revert; no persistent migration exists.
