# Approved movement boundary

The isolated rollout requires the exact opt-in `gllab:Move:277` before reading credentials. The production planner must first select Move:277 for mining level 2 and then perform its normal fresh observation and fingerprint preflight.

The action transport must reject every other character, route and request-body field. It must atomically persist an intent before sending one movement request and must reject subsequent attempts, including attempts made by a new transport after response loss. It must never delete that marker to retry.

The production StagedExecution OneShot must verify its returned state or perform read-only reconciliation. An independent character GET must report the actual destination. Read-only and default offline categories must not enable this movement.
