## ADDED Requirements

### Requirement: Explicit named reset initializes deterministic state
The MockService SHALL expose `POST /__mock/reset` with an exact supported scenario name. Resetting `basic-mining` SHALL atomically replace all process-local runtime state with a deep copy of the scenario initial state, set the virtual clock to the scenario's fixed UTC epoch, and clear the action trace. An unknown or malformed scenario MUST fail without changing the prior state, time, or trace.

#### Scenario: Known scenario resets state
- **WHEN** a client resets `basic-mining`
- **THEN** the response identifies `basic-mining`, the virtual clock equals the fixed scenario epoch, no character is yet action-ready, and the action trace is empty

#### Scenario: Unknown scenario does not mutate
- **WHEN** a client requests an unsupported scenario name
- **THEN** the request fails deterministically and subsequent state and trace reads equal their pre-request values

### Requirement: Character load initializes once and later reads retain mutations
After an explicit reset, `GET /characters/{name}` SHALL initialize the requested character from the scenario template only when that case-insensitive name is absent. Repeated reads MUST return the current snapshot and MUST NOT reset coordinates, XP, inventory, virtual time, or trace. An action attempted before reset or before character initialization MUST fail without mutation or trace append.

#### Scenario: First read initializes character
- **WHEN** `basic-mining` has been reset and the configured character is read for the first time
- **THEN** the response contains the deterministic origin state and that character becomes action-ready

#### Scenario: Repeated read preserves state
- **WHEN** the character has moved and gathered and is then read again using any casing of the same name
- **THEN** the response contains the post-gather coordinates, XP, and inventory and the existing virtual time and trace remain unchanged

#### Scenario: Action before character load is rejected atomically
- **WHEN** move or gathering is called before the character has been initialized
- **THEN** the action fails and no character state, virtual time, or trace entry changes

### Requirement: Minimal catalogs satisfy cold-cache production client contracts
The MockService SHALL expose one-page `GET /maps`, `/resources`, `/items`, and `/monsters` endpoints using the existing Artiact contract DTOs. Page 1 SHALL return deterministic scenario-defined data with internally consistent `page`, `pages`, `size`, and `total`; unsupported pages MUST fail deterministically. The catalog MUST be sufficient for `GameClient.WarmUpCache()` from an empty in-memory cache and MUST NOT read or write tracked production cache snapshots.

#### Scenario: Cold cache warms entirely from mock catalogs
- **WHEN** the production `GameClient` uses an empty in-memory cache against the in-memory `basic-mining` host and calls `WarmUpCache()`
- **THEN** maps, resources, items, and monsters are loaded and cached with the exact scenario data and no disk or external network access occurs

#### Scenario: Catalog reads are observational
- **WHEN** any supported catalog page is read
- **THEN** character state, virtual time, and action trace remain unchanged

### Requirement: Move is a deterministic atomic transition
A successful move SHALL require an initialized character and a destination present in the scenario map. It SHALL atomically update coordinates, advance virtual time by the scenario's fixed move duration, append exactly one ordered trace entry, and return an `ActionResponse` containing the updated character and completed virtual cooldown. Invalid moves MUST change none of state, time, or trace.

#### Scenario: Character moves to the copper resource
- **WHEN** the initialized character moves from the origin to the scenario's copper resource coordinate
- **THEN** coordinates equal the destination, virtual time advances by the fixed move duration, cooldown timestamps match that interval with `remaining_seconds=0`, and trace sequence 1 records only that move

#### Scenario: Unknown destination is rejected atomically
- **WHEN** a move targets coordinates absent from the scenario map
- **THEN** the request fails and state, virtual time, and trace are byte-equivalent to their prior normalized values

### Requirement: Gathering is a deterministic atomic transition
A successful gathering action SHALL require an initialized character at the scenario-defined resource location. It SHALL atomically apply the fixed mining XP and inventory delta, advance virtual time by the fixed gathering duration, append exactly one ordered trace entry, and return the updated character with completed virtual cooldown. It MUST NOT use randomness, the system clock, a real delay, or a background timer. An invalid gather MUST mutate nothing.

#### Scenario: Copper is gathered once
- **WHEN** the initialized character is at the copper resource and gathers once
- **THEN** mining XP and copper-ore inventory increase by the exact scenario amounts, virtual time advances by the fixed gathering duration, cooldown timestamps match the virtual interval with `remaining_seconds=0`, and the next trace entry records the gather

#### Scenario: Gathering at the wrong location is rejected atomically
- **WHEN** the initialized character gathers at a map without the required resource
- **THEN** the request fails and character state, virtual time, and trace do not change

### Requirement: State and trace are deterministic mock-only observations
The MockService SHALL expose `GET /__mock/state/{name}` and `GET /__mock/trace`. State responses SHALL be deep snapshots. Trace entries SHALL be immutable, 1-based, and ordered by committed action transition. Each entry SHALL contain deterministic operation, character, virtual start/end, before/after coordinates, and required deterministic deltas. Reset and reads MUST NOT appear as action entries; failed actions MUST NOT append entries. Concurrent requests MUST NOT expose partial transitions or duplicate sequence numbers.

#### Scenario: Successful slice has exact ordered trace
- **WHEN** a reset character completes move followed by gathering
- **THEN** the trace has exactly sequence 1 `move` and sequence 2 `gathering`, and each entry agrees with the corresponding committed state transition

#### Scenario: Reset and replay are reproducible
- **WHEN** reset -> load catalogs/character -> move -> gather is executed twice with the same scenario and inputs
- **THEN** normalized final state, cooldown metadata, and action trace are identical across runs

### Requirement: Virtual cooldown never causes wall-clock waiting in MockService
The MockService SHALL use only the scenario virtual clock for action timing. A successful action SHALL advance virtual time immediately and return deterministic `started_at`, `expiration`, configured `total_seconds`, `remaining_seconds=0`, and reason. MockService code and tests MUST NOT wait for the configured duration.

#### Scenario: Long configured duration completes immediately
- **WHEN** a deterministic action has a non-zero configured virtual duration
- **THEN** the response and state reflect the full virtual advance while the transition performs no real-time delay

### Requirement: MockService has no production network or secret dependency
MockService startup and tests MUST NOT load `.env`, user secrets, production credentials, production API URLs, certificates, or tracked production caches. The MockService project MUST NOT register or contain a reachable reverse proxy, forwarding middleware, outbound HTTP client, production hosted worker, or production fallback. YARP and dormant proxy code SHALL be removed. HTTP compatibility tests SHALL use only an in-memory ASP.NET host/handler and SHALL reject non-loopback test base URIs.

#### Scenario: Complete compatibility slice stays in memory
- **WHEN** the HTTP compatibility test performs reset, token, catalog/character reads, move, gather, state, and trace operations
- **THEN** every request is handled by the in-memory MockService, no socket targets the production API, no secret source is read, and no tracked cache file changes

#### Scenario: Non-loopback test destination is rejected
- **WHEN** the compatibility harness is configured with a non-loopback base URI
- **THEN** it fails before constructing a client capable of sending a request

### Requirement: Scope remains deliberately incomplete
The implementation SHALL document that `basic-mining` and its routes are the supported subset. It MUST NOT claim complete API emulation and MUST NOT add combat, rest, equipment, bank, task, market, event, multi-character, persistence, random-yield, generic simulator, or autonomous strategy behavior in this change.

#### Scenario: Unsupported capability remains absent
- **WHEN** an endpoint outside the documented subset is requested
- **THEN** MockService does not proxy or fall back to production and returns no successful emulated behavior
