# Design and boundaries

Replace the arbitrary ten-decision minimum with strictly positive decisions plus existing no-progress <= decisions and finite action/time checks. Apply the same constraint to panel preparation and form. This allows a smaller scope and does not raise any budget.

The dedicated RealApiAutonomousInspect category uses the existing read-only guard and destination validation. Extend the inspection transport with an explicit include-items option only for autonomous planning; existing manual inspection retains its original allowlist. POST always refuses. Report character name, observation/world/policy fingerprints, selected command/decision, map and inventory capacity/stock; do not emit credentials or raw character/catalog payloads.

No live action host is enabled by this change. The live operator adapter/transport allowlist, fresh named approval, full cycle, terminal restart verification and stock/cost evidence remain open acceptance tasks. R16 cannot be closed by this preparation.
