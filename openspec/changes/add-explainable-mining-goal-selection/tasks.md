## 1. Baseline and immutable decision

- [x] 1.1 Record exact HEAD, branch/status, unrelated unstaged `.serena/project.yml`, solution baseline, and existing warnings without cleaning or staging the user file.
- [x] 1.2 Add focused decision-factory and `GoalServiceTests` for every valid status/reason/fact/selected-type combination; invalid enum/reason/type/target/fact construction; policy/character precedence; nullable current level; target `19/20/21`; exact free-space `10/9`; null list/element; negative/over-capacity/overflow inventory; zero quantities; blank/null code with positive quantity; completed inventory-fact omission; repeated equality. Record initial missing-type compilation only as setup evidence.
- [x] 1.3 Add the smallest compiling immutable decision/enums/factory API plus `Evaluate(Character?)` throwing `NotImplementedException`, alongside temporary `GetGoal(ICharacterService)`; rerun until tests compile, then record RED from missing policy behavior.
- [x] 1.4 Implement factory invariants, exhaustive reason mapping, and pure evaluation; the decision stores only immutable `SelectedGoalType`, never a mutable execution `Goal`; retain old `GetGoal` only as the temporary compiling seam.
- [x] 1.5 Rerun selector/factory tests GREEN and existing suite compilation/tests; refactor only while green.

## 2. Behavioral orchestration migration

- [x] 2.1 Add a compile-only orchestration skeleton: change `IActionService.ExecuteCycleAsync` and all callers/test doubles to `Task<GoalDecision>`; make ActionService deliberately throw `NotImplementedException` after its existing pre-cancellation check without calling old `GetGoal` or executing a goal; retain `GetGoal` only as temporary compilation compatibility.
- [x] 2.2 Add and run ActionService tests requiring one character read, one `Evaluate` call, and the exact decision; assert Selected constructs one fresh `GatheringGoal(target)` for decomposition/build/execution, Completed/Blocked have no downstream calls, and pre-cancellation reads/evaluates nothing. Record behavioral RED from the deliberate skeleton throw, not compilation/mock setup.
- [x] 2.3 Implement ActionService through `Evaluate`, remove the deliberate throw and `GetGoal` after search proves no callers, and preserve cancellation/exception semantics.
- [x] 2.4 Rerun selector/ActionService and affected cancellation tests GREEN; verify no `GetGoal` reference remains; refactor only while green.

## 3. Live repeat boundaries

- [x] 3.1 Add focused StepBuilder/ActionStep/worker flow tests reproducing: move response reaches target or invalidates inventory -> zero gathers; start gather free `10` then response free `9` -> one gather; gather response reaches target -> one gather; negative level, blank/null item code with positive quantity, or other malformed inventory -> retained response, normal selected-cycle return, next-cycle Completed/Blocked, two decision events/two cycles, zero recovery delays, and no unauthorized gather. Assert selector and live checks use the same rule and resource/movement selection is unchanged.
- [x] 3.2 Run each focused case before production changes and record RED from the current two-unit predicate/ignored target.
- [x] 3.3 Pass the selected `GatheringGoal` into mining-step construction; define one non-throwing live predicate with `MiningLevel >=0 && MiningLevel < TargetLevel` and valid free inventory `>=10`; apply it through `ConditionalStep` before the first gather and as the ActionStep repeat predicate so the next normal cycle produces Completed/Blocked after authorization becomes false.
- [x] 3.4 Rerun focused step, cancellation, craft/loot, and ActionService tests GREEN; refactor only while green.

## 4. Terminal worker and single-source explanation

- [x] 4.1 Add worker tests proving Selected continues serial cycles while first-cycle Completed/Blocked stops normally after one call with no recovery delay or duplicate decision log; retain cancellation and recoverable-exception tests.
- [x] 4.2 Add focused ActionService logging/activity tests for exactly one decision event, exact conditional fields for all statuses, null current level, completed inventory omission, no-listener equivalence, and absence of inventory contents/account/credential/serialized-character data.
- [x] 4.3 Run worker/observability tests and record RED for ignored cycle results and absent decision fields.
- [x] 4.4 Implement worker terminal branching and the sole authoritative structured decision event/activity tags in ActionService; worker emits no decision event.
- [x] 4.5 Rerun worker/observability/orchestration tests GREEN; refactor only while green.

## 5. Shared configuration and documentation

- [x] 5.1 Add focused tests that call the production `AddGoalSelection(IServiceCollection,IConfiguration)` seam and prove target `20` binds while absent/non-integer/zero/negative values fail validation, without constructing `Program` or registering/starting its worker.
- [x] 5.2 Run configuration tests and record RED for the missing shared registration.
- [x] 5.3 Implement the one registration extension, call it from Program, remove duplicate goal-service registration, and add tracked `GoalSelection:MiningTargetLevel=20`.
- [x] 5.4 Rerun configuration/selector/orchestration/worker tests GREEN.
- [x] 5.5 Update `docs/README.md`, `docs/domain-model.md`, `docs/architecture.md`, `docs/development.md`, and `docs/known-limitations.md` for decision semantics, repeat boundaries, terminal worker behavior, configuration, and deferred remediation. Review `AGENTS.md` files and change them only if a durable recurring instruction is actually needed.

## 6. Verification and review

- [x] 6.1 Run exact focused selector/factory, ActionService, StepBuilder/ActionStep, worker, observability, and configuration tests; record commands/counts and RED/GREEN evidence.
- [x] 6.2 Run `dotnet build Artiact.sln --no-restore` and `dotnet test Artiact.sln --no-build --no-restore`; distinguish existing warnings from regressions.
- [x] 6.3 Do not run `RealApiOffline` unless the final diff unexpectedly touches `Artiact.RealApiTests` or shared API DTOs; always leave live tests and main host unverified.
- [x] 6.4 Run strict OpenSpec validation and `git diff --check`; verify `.serena/project.yml` remains unstaged/unmodified by this work and no `.env`, secrets, production access, cache, `bin`, `obj`, generated artifacts, new packages, or MockService changes enter the candidate.
- [x] 6.5 Search code/docs/diagrams for stale `GetGoal`, hard-coded selector target, ignored terminal decision, two-unit mining repeat, duplicate decision-log ownership, and unsafe inventory-prerequisite claims; resolve only affected references.
- [x] 6.6 Freeze the exact staged candidate with HEAD and raw binary-diff SHA-256 and obtain independent fail-closed correctness/security/OpenSpec and lean reviews. Reproduce claimed behavior defects before fixes, rerun all applicable gates, and request fresh review for every changed candidate.

Commit and push are not OpenSpec completion and require separate explicit side-effect authorization after final review.

Verification note (2026-09-05): A compiling selector skeleton produced the behavioral RED before policy implementation, and a compiling orchestration skeleton produced the behavioral RED before decision branching. Step-slice worker/event assertions reached GREEN after their dependent worker/telemetry implementation in slice 4; no compilation or harness failure was counted as behavioral RED. Final exact commands, counts, staged candidate identity, review verdict, warnings, and unverified scope are recorded in the implementation handoff.
