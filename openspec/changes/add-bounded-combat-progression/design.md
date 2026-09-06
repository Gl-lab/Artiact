## Context

Base: 05f7944. Follow ADR 0001 and docs/research/combat-equipment/epic-6-handoff.md. Preserve existing mining behavior and the original mock fixture. No production host is started during validation.

## Decisions

1. Implement prerequisites before enabling combat. A shared single-dispatch action boundary applies to all mutating client calls because an ambiguous move/gather/craft is also unsafe to replay. Transport exceptions, timeouts, server failures and malformed successful responses have unknown outcome; non-server rejection is typed and never retried. Authentication failure before action dispatch must stop the action.
2. Preserve current ActionResponse consumers through an adapter: fight parses the current characters/fight envelope, requires exactly one ordinal configured-name match, and exposes that authoritative snapshot as Data.Character. Retain fight/rest/equipment detail for policy postconditions. Equipment uses dedicated named-slot request contracts and one-element arrays; migrate legacy callers explicitly.
3. Combat is application-local and opt-in, separate from the default mining worker until deterministic acceptance. CombatLevelGoal has a positive target; nullable legacy LevelUpGoal remains inert. Each decision dispatches zero or one command, charges budget before I/O, retains returned state before checking cancellation, and makes terminal outcomes sticky.
4. Normalize required stats with presence checks; unsupported effects, conditions, access, opponent tier and arithmetic domains block. Apply ADR conservative survival bound and cap. Pre-owned equipment ranking is character/opponent aware, with separate unequip/equip commands and authoritative inventory reconciliation.
5. Default acceptance fixture: researcher HP20/attack10 versus HP20/attack3, target2, maps1→2; Move/Fight/Rest/Fight/Completed, 29 virtual seconds, level2/XP0/HP14/free8/map2. Gear fixture additionally owns quick_blade/heavy_blade; choose quick_blade independent of item level, preserve the old weapon, seven decisions/35 seconds. Use independent full payload oracles.
6. Add loot/craft only after this HTTP loop passes. One missing leaf, reciprocal probability, path-local recursion detection, reservation/conservation, explicit workshop travel and equipment postconditions. Do not expand into generic inventory remediation.

## Failure and verification strategy

Before each production slice, add behavior tests and record actual RED or already-GREEN. Cover missing/duplicate fight identity, malformed envelopes, HTTP timeout/network/5xx/rejection, no action after failed authentication, unsupported observations/access, wrong movement, missing gear/space, failed recovery, defeat, no XP, budgets, cancellation and response loss. Run focused tests, build, solution tests, RealApiOffline for contract changes, deterministic replay, strict OpenSpec and diff checks. Review exact final diff and record unresolved scope accurately.

## Risks

Changing all action failures affects the mining worker recovery path: unknown outcomes must terminate repetition, not just remove a retry loop inside GameClient. Legacy synthetic fight envelopes are not current-API evidence. Research fragment probes may need updating when the formerly missing shape is implemented; retain historical evidence as dated research.
