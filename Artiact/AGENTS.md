# Main application instructions

R20: ScheduleWorker and manual controls are mutually exclusive. Reserve full per-run ceilings durably before shared StagedExecution; never refund across RunIds/restart. Interrupted reservations and unsafe checkpoints require intervention. Keep stable local event IDs and default-off series policy. See ../docs/bounded-schedule.md.

R17: operator snapshots never acquire the execution lease or call the game API. The operator panel supports trusted-network server access without login; preserve same-origin/CSRF POST guards, bounded reads, projected data, optional observation timestamps and worker/host distinction. See ../docs/operator-panel.md.

R18: ControlsEnabled requires Inspect startup and suppresses the automatic worker. Delegate all execution to StagedExecution; preserve fresh profile-bound receipts, single ownership, CSRF/origin guards and archive restrictions. A browser disconnect is not cancellation.

R14 CombatDiscovery preserves explicit combat/gear/production permissions, Recovery allowances and combat:materials charges. Route-opening parents retain their opponent/value through equip. See ../docs/combat-goal-discovery.md; the combat ADR live no-go remains.

R22: discovery-v2 keeps the original target/reason for bounded intermediate milestones. Legacy discovery-v1 remains readable but must not execute under the new algorithm; never auto-migrate pending intent.

R11/R12: autonomous gathering uses Inspect or durable Bounded execution. Preserve version-1 manual identity, version-2 active goal/history, pending baseline context, explicit bank permission, bounded generation and zero recipe utility without a proven need. Stopped is distinct from milestone completion and Blocked. See ../docs/autonomous-goals.md.

R13 Recovery is optional. Preserve explicit Use/Rest/Craft/bank permissions, global recovery:use/materials charges, HP outcome measurement and parent completion before refill. Internal reserve-only production may have no bank; never infer permission to transfer stock. See ../docs/autonomous-recovery.md.

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
