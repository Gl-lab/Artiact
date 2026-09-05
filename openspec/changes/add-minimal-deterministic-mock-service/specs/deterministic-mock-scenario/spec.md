## ADDED Requirements

### Requirement: The basic-mining scenario literals are normative
The implementation fixture and tests SHALL reproduce the following literals exactly and MUST NOT select alternative values.

The canonical character name is `MockHero` and character-name matching uses `StringComparer.OrdinalIgnoreCase` while every response and stored snapshot preserves canonical casing. The initial character SHALL contain: `name=MockHero`, `account=mock`, `skin=men1`, `level=1`, `xp=0`, `max_xp=150`, `gold=0`, `speed=100`; every skill (`mining`, `woodcutting`, `fishing`, `weaponcrafting`, `gearcrafting`, `jewelrycrafting`, `cooking`, `alchemy`) has level 1, XP 0, and max XP 150; `hp=120`, `max_hp=120`, `haste=0`, `critical_strike=5`, `wisdom=0`, `prospecting=0`; `attack_earth=4` and the other attack fields are 0; every `dmg*` and `res_*` field is 0; `x=0`, `y=0`, `cooldown=0`, `cooldown_expiration=2000-01-01T00:00:00Z`; `weapon_slot=wooden_stick`; every other equipment, rune, utility, bag, task, and task-type string is empty; both utility quantities, task progress, and task total are 0; `inventory_max_items=20`; inventory contains slots 1 through 20 in ascending order, each with empty code and quantity 0.

The virtual epoch is `2000-01-01T00:00:00Z`. The only successful action sequence per reset is: move from `(0,0)` to `(2,0)` for virtual duration 7 seconds, then gather once for virtual duration 5 seconds, adding 6 mining XP and one `copper_ore` to slot 1. The final state is `(2,0)`, mining level 1, mining XP 6, slot 1 `copper_ore` quantity 1, virtual time `2000-01-01T00:00:12Z`, and exactly two trace entries.

The page-1 catalog envelopes SHALL use `page=1` and `pages=1`, with `size=total=data.Count`. Maps contain, in order: `{name:"Origin",skin:"forest",x:0,y:0,content:{type:"",code:""}}` and `{name:"Copper Rocks",skin:"rocks",x:2,y:0,content:{type:"resource",code:"copper_rocks"}}`. Resources contain `{name:"Copper Rocks",code:"copper_rocks",skill:"mining",level:1,drops:[{code:"copper_ore",rate:1,min_quantity:1,max_quantity:1}]}`. Items contain `{name:"Copper Ore",code:"copper_ore",level:1,type:"resource",subtype:"mining",description:"Basic mining ore.",effects:[],craft:null,tradeable:false}`. Monsters contain an empty data list.

Successful move and gather responses SHALL set `cooldown.total_seconds` to 7 and 5 respectively, `remaining_seconds=0`, `reason=mock_virtual_elapsed`, `started_at` to the pre-action virtual instant, and `expiration` to the committed post-action virtual instant. Move `details` contains XP 0 and an empty items list; gather `details` contains XP 6 and exactly `{code:"copper_ore",quantity:1}`. Move `destination` equals the normative `Copper Rocks` map. Gather destination is omitted/null as permitted by the existing nullable-at-runtime contract.

Except for move coordinates and gather mining-XP/inventory changes stated above, every `Character` field SHALL remain equal to the initial character. Because each virtual action is complete when its response is returned, `Character.cooldown` remains 0 and `Character.cooldown_expiration` remains `2000-01-01T00:00:00Z`; action timing is represented only by `ActionData.cooldown` and scenario `virtual_time`.

#### Scenario: Normative sequence reaches exact final state
- **WHEN** `basic-mining` is reset and `MockHero` is loaded, moved to `(2,0)`, and gathered once
- **THEN** every catalog, response, state field, virtual timestamp, inventory slot, and trace entry equals the normative literals above

### Requirement: Explicit named reset initializes deterministic state
The MockService SHALL expose `POST /__mock/reset` with an exact supported scenario name. Resetting `basic-mining` SHALL atomically replace all process-local runtime state with a deep copy of the scenario initial state, set the virtual clock to `2000-01-01T00:00:00Z`, and clear the action trace. An absent body, malformed JSON, non-object body, absent/null/non-string/empty/duplicate `scenario` property, or additional unsupported property MUST return `invalid_reset_request` with HTTP 400. A syntactically valid request whose string scenario value is not exactly `basic-mining` MUST return `scenario_not_found` with HTTP 404. Either failure leaves all observable state unchanged.

Generation SHALL be 0 in `Uninitialized` and increment by exactly one on each successful reset; rejected reset requests SHALL NOT increment it. Replay comparison normalizes every reset/state/trace generation by subtracting that run's successful-reset generation; all non-generation fields are compared without normalization.

#### Scenario: Known scenario resets state
- **WHEN** a client resets `basic-mining`
- **THEN** the response identifies `basic-mining`, the virtual clock equals the fixed scenario epoch, no character is yet action-ready, and the action trace is empty

#### Scenario: Unknown scenario does not mutate
- **WHEN** a client requests an unsupported scenario name
- **THEN** the request fails deterministically and subsequent state and trace reads equal their pre-request values

### Requirement: Character load initializes once and later reads retain mutations
After reset, `GET /characters/{name}` SHALL accept only a value equal to `MockHero` under `StringComparer.OrdinalIgnoreCase`. The first accepted read initializes one character from the scenario template. Every response and stored snapshot SHALL preserve the canonical name `MockHero`, regardless of route casing. Later accepted reads return the retained snapshot without mutation. An action before reset MUST return `scenario_not_initialized` with HTTP 409. After reset, move and gather SHALL validate the action-route `{name}` before character initialization: a name not ordinal-ignore-case equal to `MockHero` returns `character_not_found` with HTTP 404; an accepted canonical/case-varied name before the character's first load returns `character_not_initialized` with HTTP 409. Every such failure MUST leave state, time, generation, and trace unchanged.

#### Scenario: First read initializes character
- **WHEN** `basic-mining` has been reset and the configured character is read for the first time
- **THEN** the response contains the deterministic origin state and that character becomes action-ready

#### Scenario: Repeated read preserves state
- **WHEN** the character has moved and gathered and is then read again using any casing of the same name
- **THEN** the response contains the post-gather coordinates, XP, and inventory and the existing virtual time and trace remain unchanged

#### Scenario: Action before character load is rejected atomically
- **WHEN** move or gathering is called before the character has been initialized
- **THEN** the action fails and no character state, virtual time, or trace entry changes

### Requirement: Scenario lifecycle permits no implicit or expanded sequence
Process state SHALL begin `Uninitialized`. Except for `POST /token` and `POST /__mock/reset`, every scenario-dependent route SHALL return `scenario_not_initialized` with HTTP 409 without mutation until reset succeeds. `GET /characters/{name}` SHALL accept only `MockHero` under ordinal-ignore-case comparison, return canonical casing, and return `character_not_found` with HTTP 404 for every other name without creating state. After load the only successful sequence is one origin-to-copper move followed by one gather. A valid-shaped move outside `Ready` and a gather in `Gathered` SHALL return `invalid_transition` with HTTP 409. A gather in `Ready` at origin SHALL instead return `gathering_not_available` with HTTP 422 because location validation precedes the gather lifecycle transition. Every rejection leaves selected scenario, character, time, generation, and trace unchanged.

#### Scenario: Repeated action is rejected
- **WHEN** move is called twice or gather is called after the completed gather
- **THEN** the extra request returns `invalid_transition` with HTTP 409 and the complete observable snapshot remains unchanged

### Requirement: Minimal catalogs satisfy cold-cache production client contracts
The MockService SHALL expose one-page `GET /maps`, `/resources`, `/items`, and `/monsters` endpoints using the existing Artiact contract DTOs. Page 1 SHALL return deterministic scenario-defined data with internally consistent `page`, `pages`, `size`, and `total`; any other page MUST return `invalid_page` with HTTP 400. The catalog SHALL contain origin `(0,0)`, the copper resource at `(2,0)`, its resource/drop definition and `copper_ore` item definition, plus an empty monster list. It MUST be sufficient for `GameClient.WarmUpCache()` from an empty in-memory cache and MUST NOT read or write tracked production cache snapshots.

#### Scenario: Cold cache warms entirely from mock catalogs
- **WHEN** the production `GameClient` uses an empty in-memory cache against the in-memory `basic-mining` host and calls `WarmUpCache()`
- **THEN** maps, resources, items, and monsters are loaded and cached with the exact scenario data and no disk or external network access occurs

#### Scenario: Catalog reads are observational
- **WHEN** any supported catalog page is read
- **THEN** character state, virtual time, and action trace remain unchanged

### Requirement: Move is a deterministic atomic transition
A move request is valid-shaped only when its JSON object contains exactly one integer `x`, exactly one integer `y`, no duplicate coordinate property, and no additional property. Malformed JSON, non-object body, or absent/null/non-integer/duplicate/additional coordinates MUST return `invalid_move_request` with HTTP 400. Validation precedence is scenario initialization, ordinal-ignore-case action-route character name, canonical character initialization, request shape, lifecycle phase, then destination. In `Ready`, only `(2,0)` is accepted; every other valid-shaped coordinate returns `destination_not_found` with HTTP 422. Outside `Ready`, a valid-shaped move returns `invalid_transition` with HTTP 409. A successful move SHALL atomically update coordinates, advance virtual time by exactly 7 seconds, append exactly one ordered trace entry, and return an `ActionResponse` containing the updated character and completed virtual cooldown. Every rejected move leaves all observable state unchanged.

#### Scenario: Character moves to the copper resource
- **WHEN** the initialized character moves from the origin to the scenario's copper resource coordinate
- **THEN** coordinates equal the destination, virtual time advances by the fixed move duration, cooldown timestamps match that interval with `remaining_seconds=0`, and trace sequence 1 records only that move

#### Scenario: Unknown destination is rejected atomically
- **WHEN** a move targets coordinates absent from the scenario map
- **THEN** the request fails and state, virtual time, and trace are byte-equivalent to their prior normalized values

### Requirement: Gathering is a deterministic atomic transition
A successful gathering action SHALL require an initialized character at `(2,0)` in phase `Moved`. Gather validation precedence is scenario initialization, ordinal-ignore-case action-route character name, canonical character initialization, current location, then lifecycle phase. A gather in `Ready` at origin returns `gathering_not_available` with HTTP 422; a gather in `Gathered` at the copper coordinate returns `invalid_transition` with HTTP 409. It SHALL atomically add exactly 6 mining XP and one `copper_ore` in the first empty inventory slot or existing matching stack, advance virtual time by exactly 5 seconds, append exactly one ordered trace entry, and return the updated character with completed virtual cooldown. It MUST NOT use randomness, the system clock, a real delay, or a background timer. Every rejected gather mutates nothing.

#### Scenario: Copper is gathered once
- **WHEN** the initialized character is at the copper resource and gathers once
- **THEN** mining XP and copper-ore inventory increase by the exact scenario amounts, virtual time advances by the fixed gathering duration, cooldown timestamps match the virtual interval with `remaining_seconds=0`, and the next trace entry records the gather

#### Scenario: Gathering at the wrong location is rejected atomically
- **WHEN** the initialized character gathers at a map without the required resource
- **THEN** the request fails and character state, virtual time, and trace do not change

### Requirement: State and trace are deterministic mock-only observations
The MockService SHALL expose `GET /__mock/state/{name}` and `GET /__mock/trace`. A successful reset returns exactly one JSON object with `scenario` (string), `generation` (long), `virtual_time` (UTC round-trip string), and `trace_count` (integer). State-name matching is ordinal-ignore-case for `MockHero`, every successful response uses canonical `MockHero`, and every other name returns `character_not_found`/404. Before reset, state returns `scenario_not_initialized`/409; after reset but before the first character load, it returns `character_not_initialized`/409. A successful state read returns exactly one JSON object with `scenario` (string), `generation` (long), `phase` (one of the case-sensitive strings `Ready`, `Moved`, or `Gathered`), `virtual_time` (UTC round-trip string), and `character` (the complete existing `Character` DTO). `GET /__mock/trace` returns a top-level JSON array, empty before any successful action and after reset. Trace entries use the exact JSON fields `sequence` (long), `generation` (long), `action` (`move` or `gathering`), `character` (`MockHero`), `virtual_started_at`, `virtual_completed_at`, `duration_seconds`, `from_x`, `from_y`, `to_x`, `to_y`, `mining_xp_delta`, `item_code` (nullable), and `item_quantity_delta`. Move uses deltas `0/null/0`; gather uses `6/copper_ore/1`. Collections retain normative order; UTC values serialize with invariant round-trip `O` format. Replay equality SHALL deserialize declared DTOs and compare every declared field in order; ambient IDs, real timestamps, hashes, unordered dictionaries, and omitted/default variability are forbidden. Reset and reads MUST NOT appear as action entries; failed actions MUST NOT append entries.

All reset, initialization, state read, trace read, catalog read, move, and gather operations SHALL be linearizable through the same store synchronization boundary, and each response SHALL represent its committed snapshot. Deterministic replay is guaranteed only for non-overlapping requests in the same explicit order under the declared generation normalization. Overlapping requests SHALL remain linearizable and free of partial state and duplicate sequence numbers, but scheduler-dependent commit order is outside the replay guarantee.

#### Scenario: Successful slice has exact ordered trace
- **WHEN** a reset character completes move followed by gathering
- **THEN** the trace has exactly sequence 1 `move` and sequence 2 `gathering`, and each entry agrees with the corresponding committed state transition

#### Scenario: Reset and replay are reproducible
- **WHEN** reset -> load catalogs/character -> move -> gather is executed twice with the same scenario and inputs
- **THEN** normalized final state, cooldown metadata, and action trace are identical across runs

#### Scenario: Overlapping operations remain linearizable
- **WHEN** reset competes with an action, a read competes with an action, or two actions compete
- **THEN** each response corresponds to one complete serial commit order and no response exposes partial state or duplicate trace sequence

### Requirement: Virtual cooldown never causes wall-clock waiting in MockService
The MockService SHALL use only the scenario virtual clock for action timing. A successful action SHALL advance virtual time immediately and return deterministic `started_at`, `expiration`, configured `total_seconds`, `remaining_seconds=0`, and reason. MockService code and tests MUST NOT wait for the configured duration.

#### Scenario: Long configured duration completes immediately
- **WHEN** a deterministic action has a non-zero configured virtual duration
- **THEN** the response and state reflect the full virtual advance while the transition performs no real-time delay

### Requirement: MockService has no production network or secret dependency
MockService startup and tests MUST NOT load `.env`, user secrets, production credentials, production API URLs, certificates, or tracked production caches. The MockService project MUST NOT register or contain a reachable reverse proxy, forwarding middleware, outbound HTTP client, production hosted worker, or production fallback. YARP, unused Swagger/OpenAPI packages and registration, and dormant proxy code SHALL be removed. Manual Kestrel hosting SHALL use only `ListenLocalhost(5000)`; HTTPS/IIS Express launch profiles and SSL-port settings SHALL be removed, and environment or command-line URL configuration MUST NOT create a non-loopback listener. The compatibility authority is exactly `http://localhost` with no user-info, query, fragment, or alternate port. The compatibility factory SHALL clear default configuration sources and add only explicit in-memory non-secret settings. It SHALL supply an `HttpClient` backed directly and exclusively by the MockService `TestServer` handler; `SocketsHttpHandler`, `HttpClientHandler`, handler fallback, and socket transports are forbidden. An outer recording/authority `DelegatingHandler` SHALL validate every absolute request URI against that authority before forwarding exclusively to TestServer. `GameHttpClient` receives only sentinel `mock-user` and `mock-password`; `/token` returns sentinel `mock-token` without validating or reading Authorization. Tests SHALL record every request and prove all dispatch through the one in-memory handler.

#### Scenario: Complete compatibility slice stays in memory
- **WHEN** the HTTP compatibility test performs reset, token, catalog/character reads, move, gather, state, and trace operations
- **THEN** every request is handled by the in-memory MockService, no socket targets the production API, no secret source is read, and no tracked cache file changes

#### Scenario: Non-loopback test destination is rejected
- **WHEN** the compatibility harness is configured with a non-loopback base URI
- **THEN** it fails before constructing a client capable of sending a request

### Requirement: Rejected requests have stable local failure contracts
Every declared local failure SHALL return media type `application/problem+json`; the stable mock code SHALL be serialized as the top-level JSON extension property `code`. Tests SHALL assert HTTP status, media type, and exact `code`; framework-generated validation responses are forbidden for governed inputs. Every rejected request SHALL leave selected scenario, generation, character state, virtual time, and trace equivalent under the declared DTO comparison. Reset with absent, null, non-string, malformed, duplicate, empty, or additional properties returns `invalid_reset_request` with HTTP 400; an unknown string scenario returns `scenario_not_found` with HTTP 404. Missing, non-integer, duplicate, or non-1 `page` returns `invalid_page` with HTTP 400. Malformed move returns `invalid_move_request` with HTTP 400. Pre-reset, unknown-character, uninitialized-character, invalid-transition, unknown-destination, and unavailable-gather failures use the exact codes and statuses declared above. Any unsupported route returns `unsupported_route` with HTTP 404 and never proxies or falls back.

#### Scenario: Malformed controls are stable and atomic
- **WHEN** reset or page input is missing, null, malformed, duplicated, empty, or unsupported
- **THEN** the exact declared status/code is returned and every observable scenario/state/time/trace field remains unchanged

### Requirement: Scope remains deliberately incomplete
The only successful routes in this change SHALL be `POST /token`, `POST /__mock/reset`, page-1 `GET /maps`, `/resources`, `/items`, `/monsters`, `GET /characters/{name}`, `POST /my/{name}/action/move`, `POST /my/{name}/action/gathering`, `GET /__mock/state/{name}`, and `GET /__mock/trace`. Existing crafting behavior SHALL be removed or disabled and SHALL return `unsupported_route` with HTTP 404 without mutation. The implementation SHALL document that `basic-mining` and this allowlist are the supported subset. It MUST NOT claim complete API emulation and MUST NOT add combat, rest, equipment, bank, task, market, event, multi-character, persistence, random-yield, generic simulator, or autonomous strategy behavior in this change.

#### Scenario: Unsupported capability remains absent
- **WHEN** an endpoint outside the documented subset is requested
- **THEN** MockService does not proxy or fall back to production and returns no successful emulated behavior
