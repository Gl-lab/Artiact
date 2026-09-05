## 1. Baseline and forbidden-surface guard

- [x] 1.1 Record commit `91cabf5`, full working-tree status, solution build, 39-test baseline, and existing warnings; do not require or manufacture a clean tree.
- [x] 1.2 Create `Artiact.MockService.Tests` with xUnit, `Microsoft.AspNetCore.Mvc.Testing` 9.0.0, and project references to `Artiact` and `Artiact.MockService`; add it to `Artiact.sln`.
- [x] 1.3 Write one focused static/activation guard test that rejects YARP/proxy/forwarder registration, outbound/socket handlers, hosted workers, `.env`/user-secret/production settings, tracked-cache access, real-clock/timer/delay/random APIs, HTTPS/IIS Express profiles, non-loopback manual binding, any compatibility authority except exact `http://localhost`, and a compatibility factory not backed exclusively by `TestServer`.
- [x] 1.4 Run the guard test and record its expected RED failure against the current MockService.
- [x] 1.5 Remove only the dormant YARP/Swagger/OpenAPI packages and registration, proxy code, HTTPS redirection, and forbidden startup surface; expose the minimal public `Program` anchor and loopback-only manual binding.
- [x] 1.6 Implement the TestServer-only factory with cleared configuration, sentinel settings, per-request authority recording/guard, and no socket fallback.
- [x] 1.7 Rerun the focused guard and the affected test project GREEN; refactor only while green.

## 2. Explicit reset and lifecycle

- [x] 2.1 Write one focused reset test covering generation 0 in initial `Uninitialized`, exact `basic-mining` epoch, generation increment, empty trace, rejected-reset generation retention, and absent body, malformed JSON, non-object, absent/null/non-string/duplicate/empty/additional/unknown scenario inputs with exact status/media-type/top-level-code and atomic non-mutation.
- [x] 2.2 Run the reset test and record the expected RED feature-missing failure.
- [x] 2.3 Implement the minimum content-root `basic-mining` fixture, singleton scenario store, explicit reset parser, reset DTO, and mock Problem Details mapper.
- [x] 2.4 Rerun reset tests and the affected suite GREEN; refactor only while green.
- [x] 2.5 Write one focused lifecycle test proving every scenario-dependent route fails pre-reset, only canonical `MockHero` initializes once under ordinal-ignore-case matching, unknown action-route names return `character_not_found` before character-initialization checks, and case-varied canonical action names return `character_not_initialized` before first load.
- [x] 2.6 Run the lifecycle test and record the expected RED failure.
- [x] 2.7 Implement initialize-once canonical character loading and exact `scenario_not_initialized`, `character_not_found`, and `character_not_initialized` behavior.
- [x] 2.8 Rerun lifecycle/reset tests GREEN; prove repeated reads preserve all state/time/trace fields; refactor only while green.

## 3. Minimal catalog compatibility

- [x] 3.1 Write one focused HTTP test asserting every normative field and ordering for page 1 of maps/resources/items/monsters plus exact `invalid_page` HTTP 400 for missing, non-integer, duplicate, zero, negative, and greater-than-one page values.
- [x] 3.2 Run the catalog test and record the expected RED route-missing failure.
- [x] 3.3 Implement only the four one-page catalog endpoints and explicit page parser using existing contract DTOs.
- [x] 3.4 Rerun the catalog test and affected suite GREEN; verify catalogs do not change generation/state/time/trace; refactor only while green.
- [x] 3.5 Write one focused real-`GameClient.WarmUpCache()` test with empty in-memory `ICacheService`, exact four misses/saves, no disk cache instance, and no catalog HTTP request on the second warm-up.
- [x] 3.6 Run the compatibility test before any compatibility-specific correction. If it fails, record focused RED evidence, implement only the minimum correction, and rerun GREEN. If the endpoint tests already satisfy it, record the initial GREEN result and make no production change; never manufacture a failure.
- [x] 3.7 If and only if task 3.6 is RED, make the compatibility test GREEN solely through MockService/test-harness changes; changes to `GameClient`, `GameHttpClient`, `ICacheService`, shared DTOs, or production worker behavior are out of scope.
- [x] 3.8 Rerun focused and affected tests GREEN; refactor only while green.

## 4. Deterministic move

- [x] 4.1 Write one focused move test asserting exact response DTO, 7-second virtual advance, `remaining_seconds=0`, reason `mock_virtual_elapsed`, unchanged `Character.cooldown` fields, state snapshot, trace sequence/deltas, unknown/case-varied action-name precedence, malformed/non-object/absent/null/non-integer/duplicate/additional coordinate `invalid_move_request` cases, the exact validation precedence, `destination_not_found`, and `invalid_transition` for wrong/repeated moves with complete atomic non-mutation.
- [x] 4.2 Run the move test and record the expected RED failure.
- [x] 4.3 Implement only the normative `Ready -> Moved` store transition and thin controller mapping under the shared synchronization boundary.
- [x] 4.4 Rerun move/lifecycle tests GREEN; refactor only while green.

## 5. Deterministic gathering

- [x] 5.1 Write one focused gather test asserting exact response DTO, 5-second virtual advance, +6 mining XP, slot-1 `copper_ore` quantity 1, state snapshot, trace sequence/deltas, unknown/case-varied action-name precedence, Ready-at-origin `gathering_not_available`, repeated-gather `invalid_transition`, validation precedence, and atomic non-mutation.
- [x] 5.2 Run the gather test and record the expected RED failure.
- [x] 5.3 Implement only the normative `Moved -> Gathered` store transition with deterministic inventory-slot behavior and thin controller mapping.
- [x] 5.4 Rerun gather/move/lifecycle tests GREEN; refactor only while green.

## 6. State, trace, replay, and concurrency

- [x] 6.1 Write one focused observation test for exact reset/state/trace JSON DTO fields, canonical character and inventory ordering, invariant round-trip UTC values, immutable deep snapshots, and read operations that do not mutate or trace.
- [x] 6.2 Run the observation test and record the expected RED failure.
- [x] 6.3 Implement only the declared observation DTOs and `/__mock/state/{name}` plus `/__mock/trace` mappings.
- [x] 6.4 Rerun observation/transition tests GREEN; refactor only while green.
- [x] 6.5 Write one focused replay test that executes the complete non-overlapping sequence twice and compares every declared deserialized field and ordered trace entry.
- [x] 6.6 Run the replay test before any replay-specific correction. If it fails, record focused RED evidence, implement only the minimum correction, and rerun GREEN. If prior slices already satisfy it, record the initial GREEN result and make no production change; never manufacture a failure.
- [x] 6.7 If and only if task 6.6 is RED, implement the minimum cloning/ordering/serialization correction required for identical replay, then rerun focused and affected tests GREEN.
- [x] 6.8 Write focused concurrency tests for reset versus action, read versus action, and two competing actions, asserting linearizable committed snapshots, no partial state, and no duplicate trace sequence.
- [x] 6.9 Run concurrency tests before any synchronization correction. If they fail, record focused RED evidence, implement only the minimum correction, and rerun GREEN. If prior slices already satisfy them, record the initial GREEN result and make no production change; never manufacture a failure.
- [x] 6.10 If and only if task 6.9 is RED, implement the minimum synchronization correction required, then rerun all MockService tests GREEN; do not promise scheduler-independent order for overlapping requests.

## 7. Complete client-contract slice and unsupported surface

- [x] 7.1 Write one focused in-memory HTTP test for reset -> token -> cold catalogs -> character -> move -> gather -> state/trace using real `GameHttpClient`/`GameClient`, exact sentinel credentials/token, and a recorder proving every request used the single TestServer handler.
- [x] 7.2 Include all final-state, cooldown, trace, repeat-replay, no-socket, no-disk, and no-production-worker assertions before running the test.
- [x] 7.3 Run the complete slice before any integration-specific correction. If it fails, record focused RED evidence, implement only the minimum correction, and rerun GREEN. If prior slices already satisfy it, record the initial GREEN result and make no production change; never manufacture a failure.
- [x] 7.4 If and only if task 7.3 is RED, implement only missing MockService/test-harness behavior, then rerun the complete slice and affected suite GREEN.
- [x] 7.5 Write a focused route-allowlist test asserting existing crafting and every other unsupported action/route return exact `unsupported_route` HTTP 404 `application/problem+json` with no mutation, proxy, or fallback.
- [x] 7.6 Run the allowlist test and record the expected RED success of the legacy crafting route; remove/disable crafting and add the minimum local fallback mapping, then rerun GREEN.

## 8. Documentation and final verification

- [x] 8.1 Update `docs/mock-service.md` with normative endpoints, scenario lifecycle, state/trace schemas, virtual cooldown divergence, stable local errors, unsupported scope, TestServer boundary, and loopback-only manual host.
- [x] 8.2 Update `docs/development.md` with focused and complete MockService test commands and the new default-suite count.
- [x] 8.3 Run all MockService tests and record exact totals.
- [x] 8.4 Run `dotnet build Artiact.sln --no-restore` and `dotnet test Artiact.sln --no-restore`; distinguish baseline warnings from regressions.
- [x] 8.5 Run the complete deterministic scenario twice and programmatically compare every normalized state/cooldown/trace field.
- [x] 8.6 Run `git diff --check`; verify `.env` remains ignored/untracked; verify `Artiact/cache` hashes/status are unchanged; scan the exact diff for secrets, production URLs, proxy/outbound/socket code, generated files, real-time/random APIs, and hidden credential/config loaders.
- [x] 8.7 Run `npx -y @fission-ai/openspec@1.12.0 validate add-minimal-deterministic-mock-service --strict` and verify all four artifacts are complete.
- [x] 8.8 Obtain independent fail-closed review of the exact final staged diff for contract fidelity, deterministic/linearizable state, trace ordering, virtual-time semantics, TestServer isolation, absence of production networking, and unnecessary complexity; reproduce each blocker RED, fix GREEN, rerun gates, and request fresh review.

Commit and push are outside OpenSpec completion and require separate explicit side-effect authorization after final approval; requirements approval never authorizes publication.

## Recorded verification evidence

The implementation session recorded these focused TDD outcomes before production corrections:

| Slice | First observed result | Green result |
|---|---|---|
| forbidden startup surface | RED: the guard detected the existing YARP/Swagger/HTTPS surface | focused guard passed after removal |
| reset | RED: `/__mock/reset` was absent | reset suite passed after the explicit parser/store was added |
| character lifecycle | RED: character GET succeeded before reset | lifecycle suite passed after reset-gated initialize-once loading |
| catalogs | RED: `/maps?page=1` returned route-missing | catalog suite passed after four page-1 routes were added |
| move | RED: legacy split cache could not execute the reset-loaded character | move suite passed after the shared-store transition was added |
| gather | RED: the legacy gather path did not produce the required transition | gather suite passed after virtual-time inventory/XP mutation was added |
| state | RED: `/__mock/state/{name}` was absent | observation/action suite passed after the state mapping was added |
| route allowlist | RED: the legacy crafting endpoint remained reachable | allowlist suite passed after crafting removal and local fallback mapping |
| real `GameClient` compatibility | initial harness error was corrected; first behavior-valid run was GREEN, so no compatibility-only production change was made | compatibility test passed through one TestServer handler |
| replay and concurrency | first runs were GREEN after the shared lock/clone design, so no scheduler-order feature was added | replay and all three concurrency cases passed |
| review regression: loaded proxy configuration | RED: guard found `ReverseProxy` in MockService appsettings | appsettings were removed and ambient configuration sources cleared |
| review regression: wrong HTTP methods | reviewer prediction was not reproduced: focused test was GREEN 3/3 because `MapFallback` handled method mismatches | regression retained; no production change made |
| third-cycle review: independent normative oracles | user explicitly approved one additional narrow review cycle after the normal two-cycle limit; fixture-derived character/catalog expectations were replaced by typed `ExpectedScenario` DTOs containing every normative literal | focused catalog, character, and action tests were GREEN because production already matched the independent oracle; no production change made |
| third-cycle review: production authority text | the remaining authority literal existed only inside a negative source assertion | the literal was removed from all MockService test source and the non-loopback case remains `example.invalid` |
| fourth-cycle review: exact authority | user explicitly approved one final narrow review cycle; `http://localhost:80` reproduced RED because `Uri` equality normalizes the default port | the guard now compares `OriginalString` ordinally and the exact-authority test is GREEN |
| fourth-cycle review: complete gathering character | gathering-response coverage lacked a full typed `Character` comparison | the response now deserializes to `ActionResponse` and compares every field with the independent normative oracle |
| fourth-cycle review: ordered inventory | strict object equivalence did not guarantee collection position | `ScenarioAssertions.CharacterEquals` retains strict recursive equality and additionally compares every inventory slot/code/quantity by index; character, action, state, and replay tests use it |

Final exact totals and immutable diff identity are recorded by the release verification and review steps rather than generated logs committed to the repository.
