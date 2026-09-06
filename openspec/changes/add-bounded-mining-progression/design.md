## Context

See proposal.md for scope. Inspected base: `098494da5cf4a5ff9a7af451c4268abc9068cce8`; the only pre-existing local modification is `.serena/project.yml`.

- `GoalService.Evaluate` is a pure target/inventory selector; its semantics already satisfy Epic 3 and remain useful unchanged.
- `ActionService` logs the selector decision before StepBuilder resolves resources. Expected resolution failures therefore currently become exceptions after a Selected log.
- `StepBuilder.FindResourceCandidate` compares skill case-sensitively with an enum string, selects highest resource before checking maps, and can dereference null. `MapService` takes the first content match. Mining's ActionStep repeats indefinitely until target or inventory blocks.
- `GoalDecomposer` contains older inventory-remediation paths; normal autonomous selection prevents entering them. Preserve independently supplied craft/loot goals, do not activate remediation to make a fixture pass.
- `MockScenarioStore` owns a single fixed definition, one lock, clone-based observations and hard-coded two-action phase/trace rules. Add scenario-specific transitions inside that existing boundary rather than an emulator/kernel project.
- `MoveStep` and `ActionStep` wait using `Cooldown.TotalSeconds`, so mock remaining_seconds=0 alone does not make real orchestration tests instant.
- Completed predecessor changes remain unarchived; their delta text is the effective specification baseline. The base bounded-orchestration spec still describes the older fixed policy. This proposal includes explicit replacement deltas for overlapping requirements.

## Goals / Non-Goals

**Goals:** A finite, inspectable mining run in the supported deterministic environment; precise normal terminal reasons; no duplicate destination selection; repeatable full-client tests.

**Non-Goals:** Real-world pathfinding/access proof, optimal XP/hour, production rollout, bank/craft remediation, generic workflow machinery, persistence or HTTP retry redesign. The old coarse Epic 4 prerequisite/craft-chain acceptance is deferred: mining in this slice never invokes craft prerequisites, so craft-chain fixes are not necessary to its completion.

## Decisions

### Keep pure policy and add a focused progression decision stage

Keep `IGoalService.Evaluate(Character?)` and its pure six outcomes. Add a focused mining resolver reading resources/maps through existing IGameClient methods; its pure ranking accepts catalog values and a snapshot. ActionService coordinates base decision -> progression guards -> catalog resolution -> final decision -> explanation -> execution. The first snapshot is read once for planning; live execution checks may read later snapshots. Do not move HTTP into GoalService or use exceptions for absent candidates.

Extend application-local `GoalDecision` enums/factory with six Blocked reasons: `InvalidMiningProgress`, `MiningDestinationNotReached`, `MiningNoProgress`, `MiningCycleLimit`, `InvalidMiningCatalog`, `NoMiningDestination`, plus existing reasons unchanged. No extra generic error reason is needed. New Blocked decisions have positive target, valid nonnegative below-target current level, null selected goal and null inventory facts, so they never invent validated inventory assertions. Base Selected retains its current factory and meaning; it is preliminary until progression checks pass. An immutable application-local destination value contains code, resource level, X and Y. Final Selected explanation combines that value with the unchanged Selected decision; do not retain mutable catalogs, API DTOs or Goal objects in the decision.

An application-local `ResolvedMiningGoal : GatheringGoal` carries the destination into StepBuilder. The autonomous path constructs it only after successful resolution. Existing independently supplied plain GatheringGoal callers use the same resolver for compatibility; a direct builder caller receives a clear failure if it supplied an unresolvable goal, while autonomous orchestration converts this condition to Blocked before building. Existing map services for crafting/looting remain unchanged.

Alternative rejected: resolve inside StepBuilder and throw for missing data, because it cannot produce a truthful pre-execution terminal decision. A new generic planning/result framework would exceed this slice.

The resolver validates only values successfully returned by its catalog provider. A returned null list or malformed entry produces invalid_mining_catalog; a thrown client loading/parsing exception propagates unchanged. In particular, GameClient.GetResources rejects a resources payload with data:null before returning a list, and InitializeAsync calls WarmUpCache before the first decision. That cold-start case remains initialization failure with zero cycles and no fabricated Blocked event. A later loading failure consumes its already-reserved cycle attempt and follows existing worker recovery. Do not catch arbitrary exceptions or inspect message text to turn them into invalid-catalog decisions; no client parsing migration is part of this plan.

### Bound mining execution without changing generic repeat semantics

Introduce a focused mining step that owns the resolved destination and invokes the game-client Move method zero/one times followed by Gathering zero/one times. This is an application-call bound: GameClient.GetAction currently retries POST on 502/504 and network/cancellation exceptions, so one invocation can have multiple HTTP attempts and server effects. The mining step cannot interpose its live guards between those retries; no retry-policy or exactly-once guarantee is introduced. It uses the existing IStep contract, shared MiningInventory rule, authoritative state save order and returned total cooldowns. Keep generic ActionStep repeat/loot attempt semantics unchanged. Inject a small cooldown-delay abstraction into the mining step, production implementation forwarding to cancellable Task.Delay. Tests record requested durations and return immediately or control completion/cancellation explicitly; no test-mode configuration switch in production.

Before mutation and after move, check current target, inventory, XP validity, resource level eligibility and destination. A changed level after move ends this selected cycle for reselection; a mismatched returned position records a pending movement failure and suppresses gathering. If returned level/inventory/progress is invalid, preserve it and let next-cycle guard precedence choose the correct reason. Save successful returned state before cancellation; check cancellation before calling the delay abstraction. A successful gather reports before/after level and XP to run state. After-action completion is observed on the next cycle, retaining the current exactly-one-decision-per-cycle behavior.

### Run-local counters are owned by the orchestration scope

Add scoped `MiningRunState`, shared by ActionService and the mining step; no static state. Reset only after successful InitializeAsync (including a post-load cancellation check). Capture configured limits once per scope. The same `AddMiningProgression` production registration binds/validates required options, installs resolver/run state/delay and is exercised by safe DI tests; Program calls it. Tracked limits 100/3 are operational caps, not a promise to reach the tracked mining target 20.

Before resolution, apply spec precedence and consume one attempt only once guards allow proceeding. Count catalog/build/execution exceptions against MaxCycles; do not refund on cancellation or failure. No-progress tracks only successful gather responses with unchanged/decreased level-and-XP ordering; malformed responses are stored and blocked next cycle. Pending movement failure remains until initialization. A finite attempt budget prevents endless normal no-op cycles as well as repeated caught failures, but cannot abort a hung tokenless call. This does not resolve unknown server outcomes or existing HTTP retries.

### Preserve basic-mining and add a separate fixed scenario

Add `MiningProgressionScenario.json` as an explicit content-root asset. Keep BasicMiningScenario.json byte-for-byte unchanged. Load both named definitions, validate each before serving, and store the active definition under the existing lock. Fixed name lookup accepts exactly the two supported scenarios; do not accept paths or arbitrary files. A new progression transition branch permits repeated moves/gathers while basic retains its current phase checks.

Reuse response/trace creation and deep-copy helpers only where they preserve basic payloads exactly. Use checked arithmetic and compute a full candidate transition before committing character, phase, clock and trace under the lock. Fixture validation covers unique codes/coordinates/slots, valid resource-to-item/map references and valid starting XP/capacity. The small synthetic XP award routine can be unit-tested with larger awards without exposing a new HTTP control. Direct store/transition fixtures can cover capacity failures; no mutation test endpoint is added.

### Exact end-to-end oracle

| Cycle | Selected resource | Mutations | Resulting level/XP | Virtual seconds |
|---|---|---|---|---|
| 1 | copper_rocks | move (2,0), gather | 1/6 | 12 |
| 2 | copper_rocks | gather | 2/2 | 17 |
| 3 | iron_rocks | move (4,0), gather | 2/8 | 29 |
| 4 | iron_rocks | gather | 3/4 | 34 |
| 5 | none | none; Completed | 3/4 | 34 |

Construct real GoalService, resolver, ActionService, StepBuilder, CharacterService and clients in the test scope; exercise the worker class with the existing safe scoped-service harness, not the main Program/host. Use the existing mock WebApplicationFactory/TestServer and in-memory cache. The fixture target is test policy 3, not a changed production target. Assert exactly five decision events, six trace entries, cooldown delay requests [7,5,5,7,5,5], final inventory order and no sixth cycle. A separate bounded manual cycle driver fails tests if more than five cycles occur. Replay uses a fresh scope after reset and compares every declared field after normalizing generation only. Cancellation tests control the injected delay, not real elapsed time.

This success-path oracle assumes successful retry-free responses; it does not characterize unknown server outcomes. At the TestServer recording handler, capture successful action response bodies without consuming or altering what the real client receives. Compare their complete ActionResponse DTOs with independent literal oracles, not only replayed state and trace. A move returns details XP 0/items [] and the exact destination map. A gather returns destination null, details XP 6 and the single ore appropriate to its location, even when character XP rolls over. Add separate direct HTTP cases for moving to Origin and for iron gathering across level-up. Keep response, fixture and transition code out of expected-value construction; retain ordered inventory/item assertions.

### External contract evidence and limitation

On 2026-09-06, [official skills documentation](https://docs.artifactsmmo.com/concepts/skills) states that gathering requires a resource tile and awards no XP when the character is ten or more levels above the resource. Use only that eligibility boundary here; ranking by resource level is a deterministic heuristic, not a formula for optimal throughput. Synthetic 6-XP awards and ten-XP thresholds are deliberately independent of upstream formulas.

[Official maps documentation](https://docs.artifactsmmo.com/concepts/maps_and_movement) identifies maps by layer/coordinates or map ID and describes access conditions. Current local MapPlace/Character/MoveRequest cannot express this complete model. This change selects catalog coordinates only and rejects duplicate coordinates instead of guessing across layers. No API DTO, route or request-body change is proposed. Modern map/access support and current OpenAPI payload compatibility must be a separate change before claiming real-world reachability or approving production use. Live/API smoke is outside verification here.

## Risks / Trade-offs

- More decision cycles/log events -> intentional one-gather granularity, bounded by explicit policy; terminal event remains separate from last action.
- Pure Selected differs from final Blocked -> log only final decision; retain pure selector tests, replace affected exact-identity orchestration assertions.
- Existing explainable spec forbids changing resource/mock behavior -> included deltas replace those restrictions explicitly; synchronize completed predecessors first.
- Finite budget can stop a productive run before target -> report mining_cycle_limit with attempts/limit; restart deliberately resets the run.
- Synthetic scenario passing is not live compatibility -> retain the explicit coordinate-only and virtual-XP limitations in docs and handoff.
- Broad simulator refactor could break basic -> retain independent literal oracles, basic regression tests and no new endpoints/packages.

## Migration Plan

1. During implementation, verify and synchronize the completed predecessor specs without asserting unverified completion or silently archiving work. Apply this change's overlapping deltas after those predecessors.
2. Deliver the five buildable slices in tasks.md with code, behavior tests and relevant docs together. No main-host invocation is needed.
3. Review the final diff, run offline gates and leave implementation tasks unchecked until actually complete. Commit/publication is separate from this planning task.
4. Rollback by reverting the eventual implementation change, including its settings/DI and added fixture; no data migration or persisted state exists. Do not alter the original basic fixture or tracked cache.
