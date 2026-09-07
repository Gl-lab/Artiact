# Requirements

- Verified outcomes MUST record actual progress and ingredient/food costs without treating a transfer as creation. Unknown outcomes MUST NOT add learned samples.
- Cooldown, observed HTTP/processing time, cooldown wait and total run elapsed MUST remain distinguishable; missing interrupted measurements MUST remain unknown.
- Candidate scoring MUST retain future prerequisite cost, account for measured useful progress and expose uncertainty/context. Changing skills/equipment/catalogs MUST NOT blindly reuse old timing/progress.
- Search MUST stay finite, preserve feasibility and existing safety/budget gates, and retain hysteresis across restart.
- Deterministic comparisons MUST use identical starts/goals/budgets and include a win, tie and loss. Report completion, actions, cooldown, active wall time, switches and preparation/material cost.
- R7–R9 real-client scenarios MUST remain green with journal attribution checks; no live gain or optimality claim.
