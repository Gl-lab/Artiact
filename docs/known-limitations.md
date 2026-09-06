# Known limitations

This list records behavior visible in the current source. It is not a roadmap and does not imply authorization to fix unrelated items.

## Planning and domain

- Goal selection supports only one configured mining milestone. The deterministic MiningDestinationResolver ranks eligible catalog coordinates each cycle. There is no generic optimizer or autonomous inventory remediation. Blocked/Completed stops the worker; external changes are not polled and restart is required to reevaluate.
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

- Dated [combat contract research](research/combat-equipment/contract-matrix.md) and offline fragment probes establish gaps against OpenAPI 8.2.3: fight returns `data.characters`/`data.fight`, equip/unequip require arrays with named slots, and map content is nested in `interactions`. Fight participant adaptation and named equipment arrays are now implemented, with rest/equipment details retained. The explicit combat session now normalizes a fire-only, effect-free subset and validates standard-access map identities; broader mechanics and live compatibility remain unverified.
- The legacy loot resolver now ranks positive reciprocal drop rates ascending with an ordinal code tie-break. Its level-only eligibility remains unsuitable as a standalone live combat safety policy.
- [ADR 0001](decisions/0001-combat-viability-and-recovery.md) proves only a narrow synthetic offline model. Missing complete effects/conditions/stat normalization, map access and ambiguous-action reconciliation prevent live combat readiness. Official sources disagree on rest timing; research uses returned cooldowns.

- Only one distinct missing loot leaf is supported per target.
- Legacy combat eligibility is approximated by `monster.Level <= character.Level + 1`; equipment, HP, resistances and recovery are not evaluated.
- The legacy resolver picks the reachable eligible monster with the lowest positive numeric reciprocal rate; distance and combat cost are ignored.
- Legacy fight execution is bounded to ten attempts and stops on a returned defeat, retaining the authoritative character without revenge or recovery. There is no rest/heal/death-recovery flow.
- Execution trusts the API's returned inventory state and does not reserve ingredients against concurrent consumers.

## Runtime and resilience

- The background service uses one DI scope for its full lifetime.
- Mining invokes each Move/Gathering method at most once per cycle; generic craft/loot graphs can still contain several actions. Finite attempt bounds do not time out hung tokenless client calls or reconcile unknown server outcomes.
- Cancellation reaches orchestration, worker recovery and step cooldown delays, but current `IGameClient` methods remain tokenless and cannot abort an already-started HTTP call. A successful in-flight action response is saved before cancellation prevents its cooldown wait, repeat or following child.
- Action POSTs dispatch once and typed action failures stop the worker. Unknown outcomes still require external state inspection; durable reconciliation across process restart is absent.
- Token rejection now stops before action dispatch; expired Bearer token refresh remains unimplemented.
- Cache freshness and location rely on local file timestamps and process working directory.
- `/health` reports a static healthy payload and does not reflect worker/API/cache status.
- Tracing is optional for action execution and gathering decomposition; a missing activity listener does not block game behavior.
- Character state is loaded once and then refreshed only from action responses; unrelated external character changes are not polled.
- Autonomous mining returns typed catalog/destination failures. Independently supplied unresolvable gathering goals and missing crafting workshops still throw.

## Build and dependency baseline

- The solution builds with many nullable-initialization warnings in API DTOs and several warnings in `StepBuilder`. Test totals belong in dated verification evidence, not in this limitations list.
- `Program.cs` triggers ASP.NET analyzer warning `ASP0000` because it calls `BuildServiceProvider` while configuring the Zipkin exporter.
- NuGet reports `NU1902` for `OpenTelemetry.Exporter.Zipkin` 1.12.0 and advisory `GHSA-88hf-wf7h-7w4m` (moderate severity).
- These warnings predate this documentation and should not be hidden when evaluating future build output.

## Mock and operations

- MockService proves scripted combat/equipment and bounded loot/craft/equip progression through real clients. Independent legacy multi-action step graphs remain a separate compatibility path.
- The mock supports basic-mining/mining-progression and the additional scripted combat-progression/combat-equipment/combat-crafting scenarios; unsupported routes return a local 404. Legacy Swagger/YARP dependencies and configuration were removed in `8171c6e`.
- Compose uses mutable image tags and development credentials; no production deployment definition is present.
- Prometheus runs in a container but scrapes `localhost:5000`, which points back into that container rather than to a host-run Artiact process. Port 5000 is also the documented mock-service port.
- `Artiact/Dockerfile` has no dependable build context for the current multi-project layout: the repository root has no matching root project file, while an `Artiact/` context omits `Artiact.Contracts`.
- CI now runs the solution and the separate `Category=RealApiOffline` suite, but branch protection is not documented or enforced by this repository.
- Default tests now cover bounded orchestration, hosted-worker cancellation/recovery, optional tracing and step cancellation/reconciliation. They still do not cover controllers, token refresh, cache filesystem behavior, Docker or end-to-end execution.

## Documentation maintenance

When a limitation is fixed, remove or amend it in the same change. If a limitation becomes an accepted contract rather than a temporary constraint, move it to the relevant architecture or domain document.


The mining-progression mock uses synthetic six-XP awards and ten-XP thresholds. Its exact offline completion/replay is not current OpenAPI payload compatibility or production rollout evidence.
