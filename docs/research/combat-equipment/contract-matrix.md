# Combat contract matrix — 2026-09-06

Scope: Epic 5 research at base `4b49b1e19c4426aca9bcee8ac2f8d9cbebdc3888`. Public specification inspected twice during research; no authentication or game action occurred. Schema observations below are distinct from the authored-fragment deserialization probes and from live compatibility, which remains untested.

Source: [official OpenAPI](https://api.artifactsmmo.com/openapi.json), version **8.2.3**, SHA-256 of response bytes **8AD21FBCCBB04CD6568BA7C0F4FE4FEF7EAAD6984763BB7EBE780FED8267F96D**. Schema names in this document resolve under `#/components/schemas/`; operations resolve under `#/paths/` with JSON Pointer escaping. The full downloaded document is not stored. Optional does not imply nullable; normalization must preserve missing/unknown values unless the source defines a default.

## Operation coverage

All listed POSTs declare JWTBearer. Listed GETs declare no operation security; current GameHttpClient nonetheless authenticates GETs. API-specific error codes below come from each operation, not a guessed global list. Transport/auth/rate failures must additionally be handled without classifying ambiguous POSTs as rejected actions.

| Method/path | Input → response schema | Declared non-200 codes | Local boundary/status |
|---|---|---|---|
| POST `/my/{name}/action/fight` | Optional FightRequestSchema → CharacterFightResponseSchema | 498,499,598,486,497,567,422 | `GameClient.Fight`, `ActionData`: incompatible participant/result shape |
| POST `/my/{name}/action/rest` | No body → CharacterRestResponseSchema | 498,499,486,422 | `Rest`: character/cooldown retained; restoration detail absent |
| POST `/my/{name}/action/equip` | EquipSchema array → EquipmentResponseSchema | 404,498,483,499,486,478,496,491,485,484,497,422 | `EquipItem`: wrong container and slot representation |
| POST `/my/{name}/action/unequip` | UnequipSchema array → EquipmentResponseSchema | 404,498,486,491,497,478,483,499,422 | `UnequipItem`: same request mismatch |
| POST `/my/{name}/action/move` | DestinationSchema → CharacterMovementResponseSchema | 498,499,490,404,486,595,596,496,422 | `Move`: x/y only; no map identity/access reconciliation |
| GET `/characters/{name}` | Path name → CharacterResponseSchema | 404 | `GetCharacter`: partial Character DTO; presence validation missing |
| GET `/maps` | Pagination → StaticDataPage_MapSchema_ | None declared | `GetMap`: obsolete content shape and absent layers/access |
| GET `/monsters` | Pagination → StaticDataPage_MonsterSchema_ | None declared | `GetMonsters`: type/initiative absent locally |
| GET `/items` | Pagination → StaticDataPage_ItemSchema_ | None declared | `GetItems`: conditions absent locally |
| GET `/effects` | Pagination → StaticDataPage_EffectSchema_ | None declared | No catalog method in IGameClient; optional if unsupported effects block |
| POST `/simulation/fight` | CombatSimulationRequestSchema → CombatSimulationResponseSchema | 404,486,451,422 | No local client; optional member/founder feature, never invoked |

Catalog pages require data,total,page,size,pages; zero pages is permitted. GETs have no character action cooldown. Every mutating response uses CooldownSchema (total_seconds, remaining_seconds, started_at, expiration, reason); no exact timing is inferred from local fixtures. The research action port records supplied duration. Rest timing has a source conflict described in [mechanics](mechanics.md).

## Fields that control migration

| Schema | Required/optional boundary | Research result and future check |
|---|---|---|
| CharacterFightDataSchema | Required cooldown, fight, characters | Legacy `data.character` remains null; validate exact controlled participant identity, never take array element zero |
| CharacterFightSchema | Required result,turns,opponent,logs,characters | Participant result entries require character_name,xp,gold,drops,final_hp; distinguish these from full authoritative character snapshots |
| FightRequestSchema | Optional participants, at most two additional names | Solo scope sends none; raid/group semantics excluded |
| EquipSchema / UnequipSchema | Equip requires code+slot; unequip requires slot; optional quantity defaults to 1 | String ItemSlot versus Inventory's integer slot; arrays of 1–20 items; research recommends one item per command |
| EquipmentTransactionSchema | Required cooldown,items,character | Generic ActionData drops transaction items; validate actual slot/state after each separate swap step |
| CharacterRestDataSchema | Required cooldown,hp_restored,character | Generic ActionData drops hp_restored; actual HP still needs validity/progress checks |
| DestinationSchema | x,y,map_id are optional individually | Schema alone does not enforce a usable destination combination; require supported map identity in Epic 6 |
| MapSchema | Required map_id,name,skin,x,y,layer,access,interactions | Local top-level Content remains null on current payloads; InteractionSchema content/transition may be omitted or nullable |
| AccessSchema | Required type; optional nullable conditions | No proof of reachability from an x/y match; restricted/conditional/blocked access must not default to standard |
| MonsterSchema | Required combat stats, type, initiative, gold bounds and drops; effects optional | Missing Effects in legacy DTO is null; missing required scalar stats default to zero. Normalization must detect presence |
| ItemSchema | Required identity/level/type/subtype/description/tradeable; optional conditions/effects/craft | Local ItemDatum cannot enforce conditions. Craft may be nullable; no crafting is needed for the pre-owned research gear fixture |
| DropRateSchema | Required code,rate,min_quantity,max_quantity | Rate is reciprocal: smaller is more frequent. Current resolver's descending sort is a compatibility gap; unchanged here |
| CombatSimulationRequestSchema | Required characters,monster,iterations | One to three fake characters, 1–100 iterations; no guarantee for a future real fight from sampled outcomes |
| CombatSimulationDataSchema | Required results,wins,losses,winrate | Documented output only; not an observed experiment result |

## Executable evidence and disposition

`PayloadProbeTests` references the unchanged Contracts project and executes System.Text.Json against authored fragments. It confirms fight/character loss, map/content loss, equipment shape mismatch, missing-scalar defaults and retained rest/equip character state. The small ParticipantProbe verifies identity and HP only; its successful result is not complete response validation. Fragments intentionally omit unrelated required properties and cannot establish full schema compatibility.

`TargetLootingResolverTests.Resolve_ChoosesHighestRateEligibleMonster` currently characterizes descending rate selection. Do not rewrite that established behavior inside research. Epic 6 must explicitly resolve reciprocal drop probability if it reuses loot-aware crafting. Craft-chain repeated-dependency accounting remains a separate existing limitation.

No production boundary changes are made. Before Epic 6 HTTP acceptance: migrate affected DTOs and all callers/DI/Moq setups atomically, add independent complete response oracles, run RealApiOffline and solution gates, and specify an action-specific no-blind-retry boundary. Public schema inspection cannot prove atomic multi-item swaps, defeat economics, all effect interactions or idempotency; none is assumed by the experiment.
