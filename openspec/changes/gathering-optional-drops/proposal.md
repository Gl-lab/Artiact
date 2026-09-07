# Gathering with optional drops

Live Inspect found copper_rocks blocked because its catalog includes ore and optional rare drops. Support unique, positive bounded drop entries with integer rate >= 1. Require at least one guaranteed (rate 1) drop. Reserve inventory for the sum of every maximum, never assume optional drops cannot coincide.

Acceptance: valid ore-only and ore-plus-rare outcomes reconcile; missing guaranteed output, over-max quantities, unrelated changes, duplicate/malformed drops and insufficient capacity fail closed. No action endpoint or DTO changes; production recipes remain governed by their existing planner constraints.
