## Context

`Artiact.MockService` is an ASP.NET Core host with singleton `CharacterCache`, controller-local fixture loading, and action logic that reads `MockData.json` relative to the process working directory. It implements token, character, move, gathering, and crafting routes, but not the four catalog routes used by `GameClient.WarmUpCache()`. Re-reading a character reloads the JSON fixture and overwrites accumulated state. Cooldown responses are empty, there is no reset/state/trace contract, dictionary keys are case-sensitive and unsynchronized, and the project still references YARP with commented reverse-proxy code.

The approved slice is intentionally narrower than the roadmap's eventual simulator: one resettable mining scenario proving reset -> character/catalog load -> move -> gather through existing client contracts. The mock remains an incomplete local substitute, not an emulator.

## Goals / Non-Goals

**Goals:**

- Reproduce the same state and trace for the same named scenario and request sequence.
- Support a cold-cache `GameClient.WarmUpCache()` plus character load against an in-memory mock host.
- Execute one deterministic move and one deterministic gather with atomic state updates.
- Model action duration on a virtual clock without wall-clock sleeps.
- Make test and runtime fixture paths independent of the caller's working directory.
- Make production-network access absent by construction.

**Non-Goals:**

- No combat, rest, equip, bank, tasks, market, events, multi-character scheduling, persistence, random yields, or generic rule engine.
- No complete Artifacts API error envelope or authentication implementation.
- No production `Artiact` hosted-worker execution in tests.
- No refresh of tracked production cache snapshots.
- No general strategy state machine or explainable goal selection in this change.
- No broad repair of crafting, XP, or nullable warnings outside behavior touched by the mining scenario.

## Decisions

### 1. Keep one process-local scenario state service

Replace controller-owned fixture reloads and the mutable cache split with one singleton `IMockScenarioStore`. It owns the selected immutable scenario definition and a locked mutable runtime state: character snapshots, virtual time, and ordered trace. The first release contains exactly one JSON-backed named scenario, `basic-mining`, loaded from the content root rather than the process working directory.

`POST /__mock/reset` accepts exactly `{ "scenario": "basic-mining" }`, deep-copies initial state, sets the fixed UTC epoch, clears the trace, increments generation from its initial value 0, and returns a non-secret reset summary. Absent, malformed, non-object, missing/null/non-string/empty/duplicate/additional-property requests return `invalid_reset_request`/400; a valid object with an unknown string scenario returns `scenario_not_found`/404. Rejected reset requests do not change generation or any other observable state.

Runtime phases are `Uninitialized`, `Ready`, `Moved`, and `Gathered`. Process startup is `Uninitialized`; only token and reset work before reset. Reset enters `Ready` with no initialized character. Loading canonical `MockHero` makes actions available without changing phase or trace. The only successful mutation sequence is `Ready -> Moved -> Gathered`; repeated or out-of-order actions fail atomically. The capability spec's normative literal table is authoritative for fixture data and expected responses.

Alternative: introduce a reusable simulator class-library now. Rejected until a second vertical slice proves that an independently packaged transition kernel is needed.

### 2. Distinguish reset from character reads

After reset, `GET /characters/{name}` accepts only `MockHero` under ordinal-ignore-case matching and initializes it from the scenario template only if absent. Stored state and every response retain canonical `MockHero` casing regardless of route spelling. Later GETs return the current state and MUST NOT reset coordinates, XP, inventory, virtual time, or trace. Actions before reset or before character initialization fail without mutation or trace append.

This resolves the current repeated-read reset behavior while preserving the existing requirement that character GET seeds action state.

### 3. Serve the minimum one-page catalog subset

Implement `GET /maps?page=1`, `/resources?page=1`, `/items?page=1`, and `/monsters?page=1` with the existing contract DTOs and pagination metadata (`page=1`, `pages=1`, consistent `size` and `total`). The fixture uses epoch `2000-01-01T00:00:00Z`, origin `(0,0)`, copper resource `(2,0)`, move duration 7 seconds, gather duration 5 seconds, gather reward 6 mining XP and one `copper_ore`; it includes the matching map/resource/drop and item definitions plus an empty monster list. Any page other than `1` fails deterministically with `invalid_page`.

Catalog reads are immutable and do not advance virtual time or append action trace entries.

### 4. Advance virtual time atomically, never sleep

Each successful action transition has a deterministic configured duration. Under the store lock, move or gather validates preconditions, captures before-state, mutates character state, advances the virtual clock by the configured duration, and appends one trace entry. The response cooldown contains the fixed virtual `started_at`, deterministic `expiration`, configured `total_seconds`, `remaining_seconds=0`, and an operation reason. No `Task.Delay`, real clock, randomness, or background timer is used.

Returning `remaining_seconds=0` means the mock action is already complete when the HTTP response is produced. Production step code currently waits on `total_seconds`; the compatibility smoke therefore calls `GameClient` actions directly and does not start the autonomous orchestration host. Aligning production cooldown-wait semantics is a separate change if a later end-to-end worker scenario requires it.

### 5. Make state and trace observable through mock-only controls

`GET /__mock/state/{name}` returns a deep snapshot containing the current character and virtual UTC time. `GET /__mock/trace` returns an immutable ordered list of successful action entries. Each entry contains only deterministic fields: 1-based sequence, character, action, virtual start/end, before/after coordinates, and deterministic state deltas needed by tests. Reset and read operations are not action entries. Failed actions append nothing.

The endpoints are intentionally under `/__mock/*` and are documented as test controls, not production-compatible API routes.

### 6. Prohibit production networking by construction

Remove the unused YARP, Swashbuckle, and `Microsoft.AspNetCore.OpenApi` package references together with commented proxy code and unused Swagger registration. Do not load `.env`, user secrets, production API settings, certificates, or external URLs. The mock host maps local controllers only and does not register an outbound `HttpClient`, reverse proxy, hosted worker, or forwarding middleware. Remove `UseHttpsRedirection` because the supported launch profile is loopback HTTP and redirects add no safety to a local deterministic substitute. Manual startup binds Kestrel to loopback only; test startup uses `TestServer` and opens no socket.

Tests use `WebApplicationFactory`/`TestServer` (or an equivalent in-memory ASP.NET host) and a `GameHttpClient` whose factory returns only the in-memory handler. Test configuration uses non-secret literals and an in-memory `ICacheService`; it does not touch `Artiact/cache`, `.env`, or the network stack.

### 7. Return stable local errors without emulating the full production envelope

Expected failures return `application/problem+json`; the top-level extension property `code` carries the stable mock code. Codes are `scenario_not_found` (404), `scenario_not_initialized` (409), `character_not_initialized` (409), `invalid_page` (400), `invalid_move_request` (400), `destination_not_found` (422), and `gathering_not_available` (422). Validation completes before mutation; failed requests do not advance time or append trace. Full production error-envelope compatibility remains out of scope.

Additional exact codes are `invalid_reset_request` (400), `character_not_found` (404), `invalid_transition` (409), and `unsupported_route` (404). Reset, page, and move input are parsed explicitly so missing, null, malformed, duplicate, additional, and unsupported values produce these stable codes rather than framework-dependent model-binding messages. Error validation precedence is fixed by the capability spec.

### 8. Verify the complete slice through real client contracts

The HTTP compatibility test performs:

1. reset `basic-mining` through the mock control endpoint;
2. construct production `GameHttpClient`/`GameClient` with the in-memory mock handler and in-memory cache;
3. call `WarmUpCache()` from an empty cache and assert the exact minimal catalogs;
4. load the character at the deterministic origin;
5. move to the copper resource coordinate;
6. gather once;
7. read mock state and trace;
8. assert final coordinates, mining XP, inventory, virtual time, cooldown metadata, and exact two-entry move/gather trace;
9. reset and repeat the sequence, asserting byte-equivalent normalized state and trace.

The test never constructs or runs `Artiact.Program` or `ArtiactBackgroundService`.

## Risks / Trade-offs

- [Concurrent requests could interleave state and trace] -> Keep validate/mutate/clock/append inside one store lock and return deep snapshots.
- [Fixture DTOs can drift from `GameClient`] -> Deserialize through the existing contract classes and exercise the real clients in the HTTP compatibility test.
- [Test host could accidentally use a real URL] -> Supply only the in-memory handler, assert the configured base URI is loopback, and fail construction for non-loopback in the test harness.
- [Virtual cooldown metadata differs from production waiting behavior] -> Document `remaining_seconds=0` and avoid the autonomous step runner in this slice; no wall-clock wait is permitted.
- [Reset can hide unintended implicit resets] -> Only the explicit reset endpoint may clear state/trace/time; repeated reads are covered as retention tests.
- [A generic simulator abstraction may be premature] -> Keep one store and one scenario until mining and combat expose shared transition needs.

## Migration Plan

1. Keep `master` as the working branch per the user's branch-minimization preference; do not create a long-lived feature branch. Record commit `91cabf5` and complete working-tree status as the pre-implementation baseline without requiring or manufacturing a clean tree.
2. Add failing store/controller tests for reset, retention, atomic failures, virtual time, and trace.
3. Implement the minimal scenario store and mock-only controls.
4. Add failing catalog tests, then implement one-page catalog endpoints.
5. Add failing move/gather state-transition tests, then implement deterministic atomic transitions.
6. Add the failing in-memory HTTP compatibility scenario using real clients and in-memory cache, then add only the required host seams/dependencies.
7. Remove YARP/proxy remnants and HTTPS redirection after tests prove no outbound dependency is required.
8. Run focused tests, full solution build/tests twice for deterministic trace comparison, OpenSpec validation, diff/secret/network-surface scans, and independent fail-closed final review.
9. Commit and push are outside OpenSpec completion and require separate explicit side-effect authorization after final approval; requirements approval never authorizes publication. Rollback is a normal revert; all mock state is process-local.
