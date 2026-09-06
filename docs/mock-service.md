# Artiact.MockService

The additional `combat-progression`, `combat-equipment` and `combat-crafting` scenarios use a scripted kernel before the mining controllers. See [combat progression](combat-progression.md) for their reset/state/trace, fight/rest/equipment/crafting subset and divergences. The mining contracts below remain unchanged.

## Purpose

`Artiact.MockService` is a process-local deterministic strategy simulator for the smallest supported Artifacts MMO client slice:

`reset → load character/catalog → move → gather`

It is not a production proxy and not a complete MMO API emulator. It has no production URL, outbound HTTP client, credentials loader, persistent database, broker, real-time cooldown, random behavior, or production worker.

## Running manually

From the repository root:

```text
dotnet run --project Artiact.MockService/Artiact.MockService.csproj --launch-profile http
```

The only manual listener is `http://localhost:5000`, configured with `ListenLocalhost(5000)`. There is no HTTPS or IIS Express profile and no environment-based non-loopback fallback.

## Deterministic scenario

The exact accepted names are `basic-mining` and `mining-progression`. The original basic-mining sequence below and its `Artiact.MockService/BasicMiningScenario.json` fixture remain unchanged.

1. `POST /__mock/reset` with exactly `{ "scenario": "basic-mining" }` atomically clears character state and trace, sets phase `Ready`, increments `generation`, and resets virtual time to `2000-01-01T00:00:00.0000000Z`.
2. Load page 1 of maps, resources, items, and monsters.
3. `GET /characters/MockHero` initializes the canonical character once. Matching is ordinal-ignore-case; responses retain `MockHero`.
4. `POST /my/MockHero/action/move` with exactly `{ "x": 2, "y": 0 }` moves to Copper Rocks and advances virtual time by seven seconds.
5. `POST /my/MockHero/action/gathering` advances virtual time by five seconds, adds 6 mining XP, and puts one `copper_ore` in inventory slot 1.

Repeated reset of the same scenario reproduces the same normalized state and ordered trace. State transitions are serialized under one synchronization boundary.

## Endpoints

| Method and route | Behavior |
|---|---|
| `POST /token` | Returns fixed test token `mock-token`; supplied Basic credentials are ignored |
| `POST /__mock/reset` | Explicitly selects and resets `basic-mining` |
| `GET /__mock/state/{name}` | Returns scenario, generation, phase, virtual time, and deep character snapshot |
| `GET /__mock/trace` | Returns immutable committed move/gather entries in sequence order |
| `GET /characters/{name}` | Initializes once and returns the canonical character snapshot |
| `GET /maps?page=1` | Returns Origin and Copper Rocks |
| `GET /resources?page=1` | Returns the copper-rock resource definition |
| `GET /items?page=1` | Returns the copper-ore item definition |
| `GET /monsters?page=1` | Returns the normative empty monster page |
| `POST /my/{name}/action/move` | Supports only `(2,0)` from phase `Ready` |
| `POST /my/{name}/action/gathering` | Supports only the transition from `Moved` at `(2,0)` |

Every other route, including the former crafting route, returns HTTP 404 `application/problem+json` with top-level code `unsupported_route`.

## State and trace schemas

`GET /__mock/state/{name}` returns exactly:

| Field | Type | Meaning |
|---|---|---|
| `scenario` | string | Always `basic-mining` |
| `generation` | integer | Starts at 0 before initialization and increments once for each successful reset; rejected resets do not increment it |
| `phase` | string | One of `Ready`, `Moved`, or `Gathered` after reset (`Uninitialized` is internal and causes 409 rather than a state envelope) |
| `virtual_time` | round-trip UTC string | Fixed epoch plus committed virtual action durations |
| `character` | object | Deep snapshot using the existing `Character` DTO and fixture inventory order |

`GET /__mock/trace` returns a top-level array. Each entry contains exactly:

| Field | Type / nullability | Move delta | Gathering delta |
|---|---|---:|---:|
| `sequence` | integer | 1 | 2 |
| `generation` | integer | current generation | current generation |
| `action` | string | `move` | `gathering` |
| `character` | string | `MockHero` | `MockHero` |
| `virtual_started_at` | round-trip UTC string | epoch | epoch + 7 s |
| `virtual_completed_at` | round-trip UTC string | epoch + 7 s | epoch + 12 s |
| `duration_seconds` | integer | 7 | 5 |
| `from_x`, `from_y` | integers | `0`, `0` | `2`, `0` |
| `to_x`, `to_y` | integers | `2`, `0` | `2`, `0` |
| `mining_xp_delta` | integer | 0 | 6 |
| `item_code` | nullable string | `null` | `copper_ore` |
| `item_quantity_delta` | integer | 0 | 1 |

Reset atomically empties the trace. Character/catalog/state/trace reads do not append entries or advance virtual time. Returned state, catalog, character, and trace values are deep snapshots rather than mutable store references.

## Virtual cooldown divergence

Action responses keep the production `ActionResponse` DTO shape but cooldown completion is logical rather than wall-clock based:

- move: `total_seconds=7`;
- gather: `total_seconds=5`;
- both: `remaining_seconds=0`, reason `mock_virtual_elapsed`;
- timestamps come from the fixed virtual clock;
- no `Task.Delay`, timer, sleep, random source, `DateTime.Now`, or `DateTime.UtcNow` is used.

The embedded `Character.cooldown` and `Character.cooldown_expiration` remain the fixture values; the action-level cooldown records the virtual transition.

## Stable local errors

Expected failures use HTTP `application/problem+json` with a top-level `code`. Codes include:

- `invalid_reset_request` (400);
- `scenario_not_found` (404);
- `scenario_not_initialized` (409);
- `character_not_found` (404);
- `character_not_initialized` (409);
- `invalid_page` (400);
- `invalid_move_request` (400);
- `destination_not_found` (422);
- `gathering_not_available` (422);
- `invalid_transition` (409);
- `unsupported_route` (404).

Rejected operations do not alter generation, character state, virtual time, or trace.

## Test boundary

`Artiact.MockService.Tests` uses `WebApplicationFactory`/`TestServer`. The factory clears ambient configuration and adds only explicit in-memory test settings. Compatibility construction accepts only exact authority `http://localhost`; requests are recorded and delivered through the TestServer handler, so no TCP socket is opened.

The real `GameHttpClient` and `GameClient` are exercised with sentinel credentials and an in-memory `ICacheService`. The second `WarmUpCache()` performs no catalog HTTP requests and no tracked `Artiact/cache` file is used.

Run:

```text
dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore
dotnet test Artiact.sln --no-restore
```

## Deliberate non-goals

Unsupported: combat, rest, crafting, equipment, item use, recycling, deletion, bank, tasks, marketplace, multi-character simulation, real authentication, production networking, persistence, full economy/world simulation, and scheduler-independent ordering for overlapping requests.

## Mining progression scenario

Reset with exactly `{"scenario":"mining-progression"}`. `MiningProgressionScenario.json` adds Iron Rocks at (4,0), a level-2 mining resource yielding iron ore, alongside Origin and Copper Rocks. Starting character fields match basic-mining except mining max XP is 10. Both definitions are validated at load time. Reset selects the full definition, clears character/trace and resets the virtual clock atomically; switching back restores every basic-mining rule.

Moves may target any scenario map, with current coordinate rejected as invalid_transition and missing coordinates as destination_not_found. Gathering requires a mining tile, sufficient level, capacity for one unit and a matching or empty slot. Failures return gathering_not_available, insufficient_mining_level or inventory_full before mutation. Existing matching slots win over empty slots; otherwise the first empty slot in fixture order is used. Checked XP/level/time overflow returns invalid_transition without partial state, phase, time or trace changes.

Each gather awards a synthetic six XP and one local ore. Every ten XP increments mining level and preserves the remainder; max XP remains ten. These are test rules independent of upstream game formulas. Two copper gathers then two iron gathers produce level/XP 1/6, 2/2, 2/8, 3/4. With moves to copper and iron the exact cooldown requests are 7,5,5,7,5,5 seconds and virtual elapsed time is 34 seconds. Phase records the last action (Moved/Gathered) and does not prohibit later valid progression actions.

Responses contain the complete committed character, unchanged embedded character cooldown fields, total action cooldown 7/5, remaining zero and mock_virtual_elapsed. Move details are XP zero/items empty and destination is the actual map, including Origin. Gather destination is null and details report award six plus the correct one-unit ore, even across level-up. Ordered state, trace, catalogs and responses are deep snapshots. Tests compare complete independent literals, replay after reset, concurrent state/catalog reads and reset/action races. There is no new endpoint or external transport.

## Strategy portfolio scenario

The additional named strategy-portfolio reset uses researcher with the combat-equipment starting state, mine map 4 and forest map 5. It adds gathering for two professions and an old-weapon fight outcome. It preserves existing scenario responses. See [Strategy portfolio](strategy-portfolio.md) for the literal 12-action/69-second oracle and supported subset.
