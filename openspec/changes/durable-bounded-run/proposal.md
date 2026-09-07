# R2 — Durable bounded portfolio run

Add explicit Bounded execution for one character/process with persisted decisions/action/time budgets, write-ahead command intent and exclusive filesystem ownership. Existing Inspect/OneShot behavior remains available. Default remains Inspect. No live rollout or bank is included.

Acceptance: restart at intent-before-POST, response loss and response-before-save never blindly resends an action; unproven outcomes stop. Restart retains budgets, cancellation stops, and a second owner dispatches nothing. Status reports run/goal/command/budget/stop reason without payloads or credentials.
