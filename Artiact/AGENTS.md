# Main application instructions

R10: keep actual action facts separate from conservative resource charges and route estimates. Persist verified facts before wait, retain null for incomplete wait/unknown timing, and never learn XP/cooldown from reconciliation. Full-path context must change with relevant levels/equipment/catalogs. Preserve all feasible parent alternatives through consumable wrappers. See ../docs/full-path-selection.md.

This directory contains the executable ASP.NET Core host and autonomous background worker. Parent instructions in `../AGENTS.md` also apply.

## Read before changing

- Runtime and dependencies: `../docs/architecture.md`
- Domain and looting flow: `../docs/domain-model.md`
- Configuration and commands: `../docs/development.md`

## Change rules

- `Program.cs` is the DI composition root. Constructor/interface changes must be reflected there and in tests.
- `ActionService` orchestrates; domain planning belongs in the focused service/resolver, not in HTTP transport code.
- Steps must update `CharacterService` from the API response and respect cooldown semantics.
- Recursive craft planning must remain cycle-safe and must not invent reusable real inventory.
- Loot execution predicates must use live character state and remain bounded.
- New `IGameClient` actions require contract, client, step, mock/test-double and test impact review.
- Never log credentials, Basic headers, Bearer tokens or user-secret values.
- CombatSessionFactory is explicit and does not replace mining startup. Its effect-free elemental normalized subset and fire-only weapon replacement, action bounds and mock evidence are documented in `../docs/combat-progression.md`. LastCharacterPayload/LastActionPayload contain raw character state and must not be logged.

## Verification

Run the narrow test class for the changed service, then:

```text
dotnet test ../Artiact.sln --no-restore
```

Default Inspect does not dispatch actions. Explicit OneShot/Legacy settings can act; use the TestServer suite for verification. See ../docs/staged-operation.md for consent, origin and freshness guards.

R2: explicit Bounded execution now persists its journal and budgets and holds a local character lease; see [bounded operation](../docs/bounded-operation.md). Earlier in-memory restart limitations still apply to standalone Inspect/OneShot/Legacy sessions.

R8a journal entries carry conservative resource charges and refill phase; preserve these before dispatch and during pending-candidate reconstruction. Supply must not reset parent no-progress indefinitely. See ../docs/consumables.md for the supported immediate-heal subset.

R9 autonomous mode uses EquipmentProjection for static weapon/shield changes; standalone fire-only replacement remains separate. Preserve exact inventory/stat checks and active-slot rejection; see ../docs/autonomous-combat.md.
