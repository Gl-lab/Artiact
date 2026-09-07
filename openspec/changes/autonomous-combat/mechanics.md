# Mechanics matrix — 2026-09-07

Public sources read without authentication: [combat](https://docs.artifactsmmo.com/concepts/stats_and_fights/), [equipment](https://docs.artifactsmmo.com/concepts/equipment/), [OpenAPI](https://api.artifactsmmo.com/openapi.json). No live character actions.

| Mechanic | Model decision |
|---|---|
| Four independent attacks, global + elemental bonus, resistance, rounding, critical multiplier | Existing arithmetic retained; ignore player critical benefit, assume every possible monster critical |
| Initiative; finite turn limit | Assume monster hits each exchange, cap 50 exchanges; conservative bound, not exact turn simulation |
| Normal/elite/boss/raid and active monster effects | Only normal, empty effects |
| Level XP penalty | Exclude opponents at least ten levels below player; forecasts are estimates, verify actual progress |
| Defeat returns to spawn with low HP | Stop run, no automatic defeat recovery |
| Weapon and shield stat effects | Allow named static channels only; reject HP modifiers and conditions |
| Utilities and runes | Reject equipped utility/rune; automatic consumption and timed effects excluded |
| Equip/unequip arrays, slot, quantity, cooldown | Existing client array contract; add shield execution with exact response checks |

This matrix records a deliberately narrower implementation than upstream. Local scripted outcomes establish regression behavior only, and do not establish safety against live combat.
