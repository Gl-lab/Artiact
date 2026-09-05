## 1. Baseline and forbidden-surface guard

- [ ] 1.1 Record commit `91cabf5`, full working-tree status, solution build, 39-test baseline, and existing warnings; do not require or manufacture a clean tree.
- [ ] 1.2 Create `Artiact.MockService.Tests` with xUnit, `Microsoft.AspNetCore.Mvc.Testing` 9.0.0, and project references to `Artiact` and `Artiact.MockService`; add it to `Artiact.sln`.
- [ ] 1.3 Write one focused static/activation guard test that rejects YARP/proxy/forwarder registration, outbound/socket handlers, hosted workers, `.env`/user-secret/production settings, tracked-cache access, real-clock/timer/delay/random APIs, HTTPS/IIS Express profiles, non-loopback manual binding, any compatibility authority except exact `http://localhost`, and a compatibility factory not backed exclusively by `TestServer`.
- [ ] 1.4 Run the guard test and record its expected RED failure against the current MockService.
- [ ] 1.5 Remove only the dormant YARP/Swagger/OpenAPI packages and registration, proxy code, HTTPS redirection, and forbidden startup surface; expose the minimal public `Program` anchor and loopback-only manual binding.
- [ ] 1.6 Implement the TestServer-only factory with cleared configuration, sentinel settings, per-request authority recording/guard, and no socket fallback.
- [ ] 1.7 Rerun the focused guard and the affected test project GREEN; refactor only while green.

## 2. Explicit reset and lifecycle

- [ ] 2.1 Write one focused reset test covering generation 0 in initial `Uninitialized`, exact `basic-mining` epoch, generation increment, empty trace, rejected-reset generation retention, and absent body, malformed JSON, non-object, absent/null/non-string/duplicate/empty/additional/unknown scenario inputs with exact status/media-type/top-level-code and atomic non-mutation.
- [ ] 2.2 Run the reset test and record the expected RED feature-missing failure.
- [ ] 2.3 Implement the minimum content-root `basic-mining` fixture, singleton scenario store, explicit reset parser, reset DTO, and mock Problem Details mapper.
- [ ] 2.4 Rerun reset tests and the affected suite GREEN; refactor only while green.
- [ ] 2.5 Write one focused lifecycle test proving every scenario-dependent route fails pre-reset, only canonical `MockHero` initializes once under ordinal-ignore-case matching, unknown action-route names return `character_not_found` before character-initialization checks, and case-varied canonical action names return `character_not_initialized` before first load.
- [ ] 2.6 Run the lifecycle test and record the expected RED failure.
- [ ] 2.7 Implement initialize-once canonical character loading and exact `scenario_not_initialized`, `character_not_found`, and `character_not_initialized` behavior.
- [ ] 2.8 Rerun lifecycle/reset tests GREEN; prove repeated reads preserve all state/time/trace fields; refactor only while green.

## 3. Minimal catalog compatibility

- [ ] 3.1 Write one focused HTTP test asserting every normative field and ordering for page 1 of maps/resources/items/monsters plus exact `invalid_page` HTTP 400 for missing, non-integer, duplicate, zero, negative, and greater-than-one page values.
- [ ] 3.2 Run the catalog test and record the expected RED route-missing failure.
- [ ] 3.3 Implement only the four one-page catalog endpoints and explicit page parser using existing contract DTOs.
- [ ] 3.4 Rerun the catalog test and affected suite GREEN; verify catalogs do not change generation/state/time/trace; refactor only while green.
- [ ] 3.5 Write one focused real-`GameClient.WarmUpCache()` test with empty in-memory `ICacheService`, exact four misses/saves, no disk cache instance, and no catalog HTTP request on the second warm-up.
- [ ] 3.6 Run the compatibility test before any compatibility-specific correction. If it fails, record focused RED evidence, implement only the minimum correction, and rerun GREEN. If the endpoint tests already satisfy it, record the initial GREEN result and make no production change; never manufacture a failure.
- [ ] 3.7 If and only if task 3.6 is RED, make the compatibility test GREEN solely through MockService/test-harness changes; changes to `GameClient`, `GameHttpClient`, `ICacheService`, shared DTOs, or production worker behavior are out of scope.
- [ ] 3.8 Rerun focused and affected tests GREEN; refactor only while green.

## 4. Deterministic move

- [ ] 4.1 Write one focused move test asserting exact response DTO, 7-second virtual advance, `remaining_seconds=0`, reason `mock_virtual_elapsed`, unchanged `Character.cooldown` fields, state snapshot, trace sequence/deltas, unknown/case-varied action-name precedence, malformed/non-object/absent/null/non-integer/duplicate/additional coordinate `invalid_move_request` cases, the exact validation precedence, `destination_not_found`, and `invalid_transition` for wrong/repeated moves with complete atomic non-mutation.
- [ ] 4.2 Run the move test and record the expected RED failure.
- [ ] 4.3 Implement only the normative `Ready -> Moved` store transition and thin controller mapping under the shared synchronization boundary.
- [ ] 4.4 Rerun move/lifecycle tests GREEN; refactor only while green.

## 5. Deterministic gathering

- [ ] 5.1 Write one focused gather test asserting exact response DTO, 5-second virtual advance, +6 mining XP, slot-1 `copper_ore` quantity 1, state snapshot, trace sequence/deltas, unknown/case-varied action-name precedence, Ready-at-origin `gathering_not_available`, repeated-gather `invalid_transition`, validation precedence, and atomic non-mutation.
- [ ] 5.2 Run the gather test and record the expected RED failure.
- [ ] 5.3 Implement only the normative `Moved -> Gathered` store transition with deterministic inventory-slot behavior and thin controller mapping.
- [ ] 5.4 Rerun gather/move/lifecycle tests GREEN; refactor only while green.

## 6. State, trace, replay, and concurrency

- [ ] 6.1 Write one focused observation test for exact reset/state/trace JSON DTO fields, canonical character and inventory ordering, invariant round-trip UTC values, immutable deep snapshots, and read operations that do not mutate or trace.
- [ ] 6.2 Run the observation test and record the expected RED failure.
- [ ] 6.3 Implement only the declared observation DTOs and `/__mock/state/{name}` plus `/__mock/trace` mappings.
- [ ] 6.4 Rerun observation/transition tests GREEN; refactor only while green.
- [ ] 6.5 Write one focused replay test that executes the complete non-overlapping sequence twice and compares every declared deserialized field and ordered trace entry.
- [ ] 6.6 Run the replay test before any replay-specific correction. If it fails, record focused RED evidence, implement only the minimum correction, and rerun GREEN. If prior slices already satisfy it, record the initial GREEN result and make no production change; never manufacture a failure.
- [ ] 6.7 If and only if task 6.6 is RED, implement the minimum cloning/ordering/serialization correction required for identical replay, then rerun focused and affected tests GREEN.
- [ ] 6.8 Write focused concurrency tests for reset versus action, read versus action, and two competing actions, asserting linearizable committed snapshots, no partial state, and no duplicate trace sequence.
- [ ] 6.9 Run concurrency tests before any synchronization correction. If they fail, record focused RED evidence, implement only the minimum correction, and rerun GREEN. If prior slices already satisfy them, record the initial GREEN result and make no production change; never manufacture a failure.
- [ ] 6.10 If and only if task 6.9 is RED, implement the minimum synchronization correction required, then rerun all MockService tests GREEN; do not promise scheduler-independent order for overlapping requests.

## 7. Complete client-contract slice and unsupported surface

- [ ] 7.1 Write one focused in-memory HTTP test for reset -> token -> cold catalogs -> character -> move -> gather -> state/trace using real `GameHttpClient`/`GameClient`, exact sentinel credentials/token, and a recorder proving every request used the single TestServer handler.
- [ ] 7.2 Include all final-state, cooldown, trace, repeat-replay, no-socket, no-disk, and no-production-worker assertions before running the test.
- [ ] 7.3 Run the complete slice before any integration-specific correction. If it fails, record focused RED evidence, implement only the minimum correction, and rerun GREEN. If prior slices already satisfy it, record the initial GREEN result and make no production change; never manufacture a failure.
- [ ] 7.4 If and only if task 7.3 is RED, implement only missing MockService/test-harness behavior, then rerun the complete slice and affected suite GREEN.
- [ ] 7.5 Write a focused route-allowlist test asserting existing crafting and every other unsupported action/route return exact `unsupported_route` HTTP 404 `application/problem+json` with no mutation, proxy, or fallback.
- [ ] 7.6 Run the allowlist test and record the expected RED success of the legacy crafting route; remove/disable crafting and add the minimum local fallback mapping, then rerun GREEN.

## 8. Documentation and final verification

- [ ] 8.1 Update `docs/mock-service.md` with normative endpoints, scenario lifecycle, state/trace schemas, virtual cooldown divergence, stable local errors, unsupported scope, TestServer boundary, and loopback-only manual host.
- [ ] 8.2 Update `docs/development.md` with focused and complete MockService test commands and the new default-suite count.
- [ ] 8.3 Run all MockService tests and record exact totals.
- [ ] 8.4 Run `dotnet build Artiact.sln --no-restore` and `dotnet test Artiact.sln --no-restore`; distinguish baseline warnings from regressions.
- [ ] 8.5 Run the complete deterministic scenario twice and programmatically compare every normalized state/cooldown/trace field.
- [ ] 8.6 Run `git diff --check`; verify `.env` remains ignored/untracked; verify `Artiact/cache` hashes/status are unchanged; scan the exact diff for secrets, production URLs, proxy/outbound/socket code, generated files, real-time/random APIs, and hidden credential/config loaders.
- [ ] 8.7 Run `npx -y @fission-ai/openspec@1.12.0 validate add-minimal-deterministic-mock-service --strict` and verify all four artifacts are complete.
- [ ] 8.8 Obtain independent fail-closed review of the exact final staged diff for contract fidelity, deterministic/linearizable state, trace ordering, virtual-time semantics, TestServer isolation, absence of production networking, and unnecessary complexity; reproduce each blocker RED, fix GREEN, rerun gates, and request fresh review.

Commit and push are outside OpenSpec completion and require separate explicit side-effect authorization after final approval; requirements approval never authorizes publication.
