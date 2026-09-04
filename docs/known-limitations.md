# Known limitations

This list records behavior visible in the current source. It is not a roadmap and does not imply authorization to fix unrelated items.

## Planning and domain

- `GoalService` always returns `GatheringGoal(20)`; there is no dynamic goal policy.
- The `GatheringGoal.TargetLevel` value is not used by `StepBuilder` when selecting a resource.
- `LevelUpGoal` exists but is not decomposed or built into steps.
- `SpendMethod.Recycle` throws `NotImplementedException`; delete is the only directly built spend step.
- `CraftTargetEvaluator` chooses the highest item level and does not use the supplied character.
- Wearable item types are a hard-coded string set.
- `CraftChainBuilder` keeps a request-wide visited set without removing completed nodes. Shared craftable dependencies can therefore be rejected even when they are not a cycle.
- If a selected target fails the finder's consumption simulation, target selection stops instead of trying the next candidate.
- Low inventory with no craftable wearable target produces an empty spend step and still proceeds to gathering.

## Looting-aware crafting

- Only one distinct missing loot leaf is supported per target.
- Combat eligibility is approximated by `monster.Level <= character.Level + 1`; equipment, HP, resistances and recovery are not evaluated.
- The resolver picks the reachable eligible monster with the highest declared drop rate; distance and combat cost are ignored.
- Fight execution is bounded to ten attempts. There is no rest/heal/death-recovery flow.
- Execution trusts the API's returned inventory state and does not reserve ingredients against concurrent consumers.

## Runtime and resilience

- The background service uses one DI scope for its full lifetime.
- `ActionService.Action()` performs five actions per call, and the worker immediately starts another batch on success.
- Worker cancellation is not propagated into API calls, retry delays or step cooldown delays.
- The action client retries operations that may be non-idempotent after network failures; the server may have completed an action before a retry.
- Token retrieval does not throw on a non-success response at the point of authentication; the later request proceeds with the previous Basic header.
- Cache freshness and location rely on local file timestamps and process working directory.
- `/health` reports a static healthy payload and does not reflect worker/API/cache status.
- `StartActivity()` returning `null` is treated as a fatal error, coupling action execution to an active trace listener.
- Character state is loaded once and then refreshed only from action responses; unrelated external character changes are not polled.
- Several mining and crafting paths assume non-empty lookup results and may throw through null dereferences, `Max`, or `First`.

## Build and dependency baseline

- The full test run passes 21 tests, but the solution currently builds with many nullable-initialization warnings in API DTOs and several warnings in `StepBuilder`.
- `Program.cs` triggers ASP.NET analyzer warning `ASP0000` because it calls `BuildServiceProvider` while configuring the Zipkin exporter.
- NuGet reports `NU1902` for `OpenTelemetry.Exporter.Zipkin` 1.12.0 and advisory `GHSA-88hf-wf7h-7w4m` (moderate severity).
- These warnings predate this documentation and should not be hidden when evaluating future build output.

## Mock and operations

- `Artiact.MockService` is incomplete and cannot execute the looting-aware fight path.
- Swagger and YARP packages/configuration are present but their middleware is not active.
- Compose uses mutable image tags and development credentials; no production deployment definition is present.
- Prometheus runs in a container but scrapes `localhost:5000`, which points back into that container rather than to a host-run Artiact process. Port 5000 is also the documented mock-service port.
- `Artiact/Dockerfile` has no dependable build context for the current multi-project layout: the repository root has no matching root project file, while an `Artiact/` context omits `Artiact.Contracts`.
- No repository CI workflow was discovered.
- Existing tests do not cover controllers, HTTP authentication/token refresh, retry behavior, cache filesystem behavior, hosted-service cancellation, configuration binding, telemetry, Docker or end-to-end execution.

## Documentation maintenance

When a limitation is fixed, remove or amend it in the same change. If a limitation becomes an accepted contract rather than a temporary constraint, move it to the relevant architecture or domain document.
