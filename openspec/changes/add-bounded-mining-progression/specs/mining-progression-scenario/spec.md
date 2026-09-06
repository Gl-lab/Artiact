## Purpose

Provide a second deterministic mock scenario that proves repeated mining, resource changes and level transitions without changing the original compatibility fixture.

## ADDED Requirements

### Requirement: Scenario selection preserves the basic-mining contract
The existing exact reset body SHALL additionally accept scenario `mining-progression`. Unknown names and invalid bodies SHALL retain existing error statuses and atomic non-mutation. Each accepted reset SHALL select the entire scenario atomically, clear loaded character and trace, advance generation once, and reset virtual time to the existing epoch. Switching either way SHALL restore the selected scenario's own catalogs and state. `basic-mining` SHALL retain its current fixture, errors, phases, one move and one gather restrictions, and exact payloads.

#### Scenario: Switching scenarios is isolated
- **WHEN** progression actions are followed by a basic-mining reset
- **THEN** basic-mining begins from its original state and second-gather rejection still holds

### Requirement: Progression fixture has an independent literal oracle
The progression character SHALL equal the basic fixture's character except mining max XP is 10; initial mining level/XP remain 1/0, capacity 20, empty ordered slots, and position (0,0). Catalogs SHALL contain Origin (0,0), Copper Rocks (2,0), and Iron Rocks (4,0), with codes `copper_rocks` and `iron_rocks`, skill `mining`, levels 1 and 2, and respective guaranteed one-unit `copper_ore` and `iron_ore` drops. Items SHALL use the basic copper item fields and an iron analogue with name `Iron Ore`, code `iron_ore`, level 2 and description `Progression mining ore.`. Iron map/resource name and skin SHALL be `Iron Rocks` and `rocks`. Other item fields SHALL match copper. Maps/resources/items SHALL be ordered as listed; one-page metadata SHALL equal row counts, and monsters SHALL retain the empty basic page.

Gathering SHALL add 6 XP at copper and 6 XP at iron, independently of level. Each level SHALL require exactly 10 XP: repeatedly subtract 10 and increment level while XP is at least 10; retain remainder and max XP 10. These SHALL be documented as synthetic test rules, not upstream game formulas. Unit transition tests SHALL cover exact threshold, carry-over and multiple levels in one award using independent literal expectations.

#### Scenario: Exact four-gather progression
- **WHEN** two copper gathers then two iron gathers succeed
- **THEN** successive level/XP pairs are 1/6, 2/2, 2/8 and 3/4, inventory slots 1 and 2 contain two copper and two iron ore respectively, and all other character fields remain unchanged except position

### Requirement: Progression actions repeat with atomic validation
The progression scenario SHALL retain initialize-once character loading, case-insensitive character names, and existing reset/name/body validation precedence. Valid moves SHALL target any of its three catalog coordinates, reject the current coordinate with `invalid_transition` (409), reject missing coordinates with `destination_not_found` (422), and use seven virtual seconds. Gathering SHALL require a mining tile, sufficient mining level, room for one unit and an existing matching or empty inventory slot. Failures SHALL respectively use `gathering_not_available` (422), `insufficient_mining_level` (422), or `inventory_full` (422), and SHALL alter nothing. Capacity/slot checks SHALL occur before mutation. Gathering SHALL use five virtual seconds, merge the matching item or use the first empty slot in fixture order, and support successive gathers and later moves.

Phase SHALL start Ready after reset, become Moved after any successful move and Gathered after any gather; these values SHALL describe the last action and SHALL NOT prohibit subsequent valid progression actions. Checked XP, inventory or time overflow SHALL reject with `invalid_transition` (409) and no partial mutation. Character/catalog/state/trace reads SHALL remain deep snapshots with no mutation or time advance.

#### Scenario: Repeat and level restrictions
- **WHEN** level 1 moves to Iron Rocks and attempts gathering
- **THEN** the move succeeds, gathering returns `insufficient_mining_level`, and the rejected gather changes neither state nor trace nor virtual time

#### Scenario: Full inventory rejects without partial XP
- **WHEN** a directly exercised progression transition has capacity exhausted or no matching/empty slot
- **THEN** gathering returns `inventory_full` with no XP, inventory, phase, time or trace changes

### Requirement: Existing HTTP envelopes and virtual trace semantics remain compatible
The additional scenario SHALL use existing routes, DTO envelopes and TestServer-only client harness. Successful action response total cooldowns SHALL remain 7/5 seconds, remaining seconds zero and reason `mock_virtual_elapsed`; embedded character cooldown fields SHALL retain fixture values. Trace entries SHALL keep existing fields with sequence starting at 1 and incrementing once per committed action, current generation, actual coordinate deltas, XP award 6 for gathers, and matching ore/quantity. The state scenario field SHALL report the active scenario. No extra endpoint, outbound client, random source, timer, real clock, configuration credential loader, disk cache or main worker SHALL be added.

Each successful response SHALL contain `data.character` as the complete committed character snapshot, including inventory order, and `data.cooldown` with `started_at` equal to that action's pre-transition virtual time and `expiration` equal to its committed post-transition time. A move SHALL return `data.details={"xp":0,"items":[]}` and `data.destination` with the exact name, skin, X, Y and content of the selected scenario map: Origin at (0,0), Copper Rocks at (2,0) or Iron Rocks at (4,0), using the literal catalog values defined above. A gather SHALL return `data.destination=null`, `data.details.xp=6` and exactly one ordered item: `{"code":"copper_ore","quantity":1}` at Copper Rocks or `{"code":"iron_ore","quantity":1}` at Iron Rocks. The XP award in details SHALL remain 6 across level-up, independently of residual `character.mining_xp`.

Contract tests SHALL compare every declared `ActionResponse` field to independent literal expected values, including details, destination, full character and cooldown. Expected values SHALL NOT be derived from the response, scenario fixture file or transition implementation. The full-client progression run SHALL inspect each successful move/gather response as well as state and trace; final-state/trace equality alone SHALL NOT constitute response compatibility evidence.

#### Scenario: Iron gather reports iron and the full XP award
- **WHEN** a character at Iron Rocks with mining level 2 and XP 8 gathers successfully
- **THEN** the response has destination null, details XP 6, exactly one iron_ore quantity 1, and character mining level/XP/max XP 3/4/10; its complete cooldown and character match independent expected values

#### Scenario: Move reports its actual destination
- **WHEN** a valid progression move targets Iron Rocks or Origin
- **THEN** the response destination matches that exact map rather than a hard-coded Copper Rocks object, details contain XP 0 and an empty item list, and the full returned character and cooldown match the committed transition

#### Scenario: Replay and concurrent observations
- **WHEN** the progression sequence is replayed after reset, or a state read races an action
- **THEN** normalized replay is identical and concurrent reads observe only complete before/after snapshots with unique committed trace sequence numbers
