# Design and evidence

Revisited ADR 0001 before implementation on 2026-09-07 against https://docs.artifactsmmo.com/concepts/stats_and_fights/. The guide explicitly calculates each element separately, applies global+elemental bonus, rounds half up, then resistance and critical. Sum independently rounded channels for minimum outgoing and maximum incoming hit; retain pessimistic exchanges.

Append immutable secondary elemental records to CombatStats with zero defaults for existing synthetic callers. Raw normalization requires explicit attack/resistance/damage fields for all elements. Effects, negative resistance, invalid domains and non-normal monsters retain rejection. Weapon replacement still changes only the fire channel; no unmodeled equipment effect becomes zero.
