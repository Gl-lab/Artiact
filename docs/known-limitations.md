# Known limitations

- R7/R8a optionally train weaponcrafting, mining, cooking and fishing prerequisites. R8 adds protected capacity-aware batches; R9 reuses them for supported equipment preparation. Other professions and standalone crafting-skill goals require separate acceptance; see [skill preparation](skill-preparation.md) and [capacity production](capacity-production.md).

- R4b supports explicit four-element arithmetic; R9 autonomous mode adds static weapon/shield projection. Standalone weapon replacement remains fire-only. Active effects, negative resistance and non-normal opponents remain unsupported; live combat remains unverified.

- R10 [full-path selection](full-path-selection.md) uses bounded estimates, not exhaustive route search. Future XP thresholds, training, travel, yields and recovery remain assumptions. Large recipe plans may exceed the estimation bound. Unknown replies, multi-level XP gaps and interrupted waits remain visible as incomplete measurement coverage. Local gain/tie/loss comparisons do not establish live improvement.

- R4a adds independent item goals and required-ingredient withdrawals ([scope](item-production.md)). It does not globally optimize capacity/route allocation or acquire arbitrary mob-drop leaves; configured budgets bound production/deposit shuttling.

- R3 adds opt-in deposits for inventory-pressure gathering; [bank scope](gathering-bank.md) covers deposits; R4a adds required-ingredient withdrawals, while gold, expansion and economic decisions remain excluded. The new banking DTO/interface is additive. Pending R2 observations from before bank fingerprint inclusion stop on mismatch rather than migrate blindly.

- R1 gathering is independent of combat normalization and supports profession-only configuration. It still accepts only same-layer standard maps without conditions/transitions and unique positive bounded resource drops with at least one guaranteed item and capacity for all simultaneous maxima. Unknown resource effects/access remain outside the supported subset; no live mining action has been verified by R1.

This list records behavior visible in the current source. It is not a roadmap and does not imply authorization to fix unrelated items.

## Planning and domain

- Default staged execution inspects a configured strategy portfolio without actions; explicit Legacy worker goal selection supports one mining milestone (see staged-operation.md and strategy-portfolio.md). The deterministic MiningDestinationResolver ranks eligible catalog coordinates each cycle. There is no generic optimizer or autonomous inventory remediation. Blocked/Completed stops the worker; external changes are not polled and restart is required to reevaluate.
- Mining destination eligibility uses the existing coordinate-only catalog. Layers, access conditions and transitions are not represented; duplicate coordinates fail validation instead of guessing a layer. A selected coordinate is not proof of real-world reachability or optimal XP/hour.
- Mining progression stops on target, inventory pressure, invalid progress/catalog, missing destination, wrong movement result, no progress or cycle limit. Finite inventory and limits do not guarantee reaching the target; automatic banking/crafting/remediation is deferred.
- `LevelUpGoal` exists but is not decomposed or built into steps.
- `SpendMethod.Recycle` throws `NotImplementedException`; delete is the only directly built spend step.
- `CraftTargetEvaluator` chooses the highest item level and does not use the supplied character.
- Wearable item types are a hard-coded string set.
- CraftChainBuilder uses path-local cycle detection and shared transactional stock; shared dependencies and sibling ingredient conservation are covered. Craft execution still trusts action responses and has no concurrent reservation service.
- If a selected target fails the finder's consumption simulation, target selection stops instead of trying the next candidate.
- Independently supplied low-inventory gathering goals can retain an empty legacy spend prerequisite. Autonomous selection blocks inventory pressure before decomposition, and live gather guards refuse invalid inventory or fewer than ten free units.

## Looting-aware crafting

- Dated [combat contract research](research/combat-equipment/contract-matrix.md) and offline fragment probes establish gaps against OpenAPI 8.2.3: fight returns `data.characters`/`data.fight`, equip/unequip require arrays with named slots, and map content is nested in `interactions`. Fight participant adaptation and named equipment arrays are now implemented, with rest/equipment details retained. The explicit combat session now normalizes a four-element, effect-free subset with fire-only weapon replacement and validates standard-access map identities; broader mechanics and live compatibility remain unverified.
- The legacy loot resolver now ranks positive reciprocal drop rates ascending with an ordinal code tie-break. Its level-only eligibility remains unsuitable as a standalone live combat safety policy.
- [ADR 0001](decisions/0001-combat-viability-and-recovery.md) proves only a narrow synthetic offline model. Missing complete effects/conditions/stat normalization, map access and ambiguous-action reconciliation prevent live combat readiness. Official sources disagree on rest timing; research uses returned cooldowns.

- Only one distinct missing loot leaf is supported per target.
- Legacy combat eligibility is approximated by `monster.Level <= character.Level + 1`; equipment, HP, resistances and recovery are not evaluated.
- The legacy resolver picks the reachable eligible monster with the lowest positive numeric reciprocal rate; distance and combat cost are ignored.
- Legacy fight execution is bounded to ten attempts and stops on a returned defeat, retaining the authoritative character without revenge or recovery. There is no rest/heal/death-recovery flow.
- Execution trusts the API's returned inventory state and does not reserve ingredients against concurrent consumers.

## Runtime and resilience

- The background service uses one DI scope for its full lifetime.
- Mining invokes each Move/Gathering method at most once per cycle; generic craft/loot graphs can still contain several actions. Concrete transport has a 30-second HTTP timeout; custom tokenless client implementations remain outside this bound. Legacy graphs do not reconcile unknown server outcomes.
- Cancellation reaches orchestration, cooldown delays and concrete GET/auth operations through operation scopes. The public IGameClient interface remains tokenless; custom implementations do not inherit those scopes. An already-dispatched POST is allowed to return within its HTTP timeout. A successful in-flight action response is saved before cancellation prevents its cooldown wait, repeat or following child.
- Action POSTs dispatch once and typed action failures stop the worker. Portfolio/OneShot supports bounded in-session read-only reconciliation. Unresolved outcomes and Legacy require external inspection; explicit Bounded supports durable read-only reconciliation across restart; standalone OneShot/Legacy do not.
- Token rejection stops before action dispatch. Read/auth refresh and transient retries share a two-send GET budget; action POSTs never replay.
- Versioned local-application-data cache has a 48-hour TTL; it is not a distributed cache or concurrent inventory reservation system.
- Staged readiness reflects probe/observation freshness and outcome, but does not continuously probe the API or report legacy worker readiness.
- Tracing is optional for action execution and gathering decomposition; a missing activity listener does not block game behavior.
- Legacy character state is loaded once and then refreshed from action responses. Portfolio ticks reread observations/preflight; changes after preflight remain an external concurrency boundary.
- Autonomous mining returns typed catalog/destination failures. Independently supplied unresolvable gathering goals and missing crafting workshops still throw.

## Build and dependency baseline

- Warning sources and the vulnerable Zipkin exporter were removed in Epic 8. CI treats build warnings as errors. Exact build/audit results belong in dated change evidence.
- Tracing now requires an OTLP receiver; the retained Compose Zipkin service is not directly compatible with the new exporter. The separate [local rollout](container-rollout.md) confirms OTLP Collector-to-Zipkin delivery.

## Mock and operations

- MockService proves scripted combat/equipment and bounded loot/craft/equip progression through real clients. Independent legacy multi-action step graphs remain a separate compatibility path.
- The mock supports basic-mining/mining-progression and the additional scripted combat-progression/combat-equipment/combat-crafting/strategy-portfolio scenarios; unsupported routes return a local 404. Legacy Swagger/YARP dependencies and configuration were removed in `8171c6e`.
- The original monitoring-only Compose uses mutable image tags and development credentials; no remote production deployment definition is present. The separate rollout profile uses pinned local acceptance images and mock credentials.
- The original monitoring-only Prometheus target `localhost:5000` points into its own container. The separate [rollout profile](container-rollout.md) uses the verified `mock:8080` application target.
- `Artiact/Dockerfile` now uses repository-root context and a non-root runtime user. Local Docker execution, non-root journal ownership and volume preservation across app recreation are now verified in [container acceptance](../openspec/changes/container-rollout/execution-evidence.md).
- CI now runs the solution and the separate `Category=RealApiOffline` suite, but branch protection is not documented or enforced by this repository.
- Default tests now cover bounded orchestration, hosted-worker cancellation/recovery, optional tracing and step cancellation/reconciliation. Transport/auth refresh, cache filesystem behavior and staged execution are covered offline. Bounded live movement/gathering and local container health/telemetry are now verified; remote production deployment, sustained load and live combat remain outside that evidence.

## Documentation maintenance

When a limitation is fixed, remove or amend it in the same change. If a limitation becomes an accepted contract rather than a temporary constraint, move it to the relevant architecture or domain document.


The mining-progression mock uses synthetic six-XP awards and ten-XP thresholds. Its exact offline completion/replay is not current OpenAPI payload compatibility or production rollout evidence.

R2: explicit Bounded execution now persists its journal and budgets and holds a local character lease; see [bounded operation](bounded-operation.md). Earlier in-memory restart limitations still apply to standalone Inspect/OneShot/Legacy sessions.

R9 local [autonomous combat](autonomous-combat.md) supports bounded stage/gear alternatives and R7/R8/R8a preparation. Only weapon/shield static effects qualify; other slots, combined loadout search, active effects and defeat recovery remain unsupported. Cooking/fishing preparation is now supported by R8a; broader profession coverage is still limited. No live combat acceptance.
