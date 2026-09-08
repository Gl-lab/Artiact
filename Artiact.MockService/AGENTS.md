# Mock service instructions

R14 combat-discovery-shield/weapon alias the R9 scripted transitions with gathering thresholds 1000. Preserve fixed-comparison 12/78 and 20/120 outcomes; see ../docs/combat-goal-discovery.md.

R12 discovery-* scenarios use the existing scripted kernel with 26-XP thresholds and 13-XP gathers, Move/Gather-only action allowlist, capacity 100 and explicit iron-map access rejection. Preserve original scenarios' XP/caps and the comparison oracles in ../openspec/changes/autonomous-goal-execution/comparisons.md.

R13 recovery-training uses the consumable training requirements with snack excluded from heal candidates. Preserve 20 actions/112 seconds, HP20 and the stock oracle in ../docs/autonomous-recovery.md.

This project is a deterministic, incomplete substitute for selected Artifacts MMO endpoints. It must not silently be described as a complete emulator or production proxy. Parent instructions in `../AGENTS.md` also apply.

Read `../docs/mock-service.md` before changes.

## Change rules

- Match `GameClient` routes and contract DTOs exactly for every implemented endpoint.
- Reset with `POST /__mock/reset` and `{ "scenario": "basic-mining" }` or `{ "scenario": "mining-progression" }`, then load `GET /characters/MockHero` once before action calls. Repeated character reads preserve mutations.
- Keep state process-local unless persistence is an explicit requirement.
- Keep the two normative fixtures in `BasicMiningScenario.json` and `MiningProgressionScenario.json`; preserve the original basic fixture byte-for-byte; preserve atomic state/trace transitions, deep snapshots and virtual cooldown semantics. Do not introduce production forwarding.
- Verify behavior through `Artiact.MockService.Tests` and its TestServer compatibility suite. Keep expected contract values independent of the fixture being tested; assert inventory ordering explicitly where contractual.
- Fixed tokens and development data are not authentication evidence.
- Add tests before relying on a new endpoint for an end-to-end main-app check.
- Combat scenarios use `CombatScenario.json` and `CombatScenarioStore`/middleware. They support `researcher`, named combat-progression/combat-equipment/combat-crafting reset, map-id movement and scripted fight/rest/equipment/crafting. Additional strategy-portfolio reset adds gathering for mining/woodcutting and old-weapon combat; its independent oracle is documented in ../docs/strategy-portfolio.md. Preserve mining reset/error behavior; see `../docs/combat-progression.md`.
- Update `docs/mock-service.md` whenever supported endpoints or deliberate divergences change.

## Run

```text
dotnet run --project Artiact.MockService.csproj --launch-profile http
```

The HTTP profile listens on `http://localhost:5000`.

The strategy-portfolio scenario alone serves GET /openapi.json from the authored StrategyOpenApiSubset.json. Preserve its explicitly partial scope; do not describe it as full upstream OpenAPI coverage.

The gathering-bank scenario also serves this partial schema and adds GET /my/bank, paginated /my/bank/items and POST bank/deposit/item. Preserve atomic inventory/bank/trace commits and the documented 13-action conservation oracle in ../docs/gathering-bank.md.

R4a's item-production/item-production-bank add bounded withdrawals and bar/tool crafting. Keep their fixtures and independent oracles aligned with ../docs/item-production.md.

R4c combat-preparation adds two simultaneous drops and their consumption by crafted_blade. Preserve the 13-action/81-second oracle and current-equipment safety test.

R7 skill-preparation/resource-preparation add synthetic weaponcrafting/mining requirements and fixed XP transitions. Preserve exact stock/command oracles in ../docs/skill-preparation.md and atomic rejection; do not describe scripted XP as the live formula.

R8 capacity-production/capacity-training constrain inventory to three units and reuse existing bank/craft transitions. Preserve ../docs/capacity-production.md oracles and never broaden the original scenarios' caps incidentally.

R8a consumable-* scenarios add immediate heal use, cooking/fishing preparation and refill. Preserve ../docs/consumables.md oracles, full item/use envelope and atomic rejection; scripted meal/snack values are not live cooked-gudgeon data.

R9 autonomous-shield/autonomous-weapon script two combat stages, a synthetic weaponcrafting shield and trained water weapon. Preserve ../docs/autonomous-combat.md oracles.
