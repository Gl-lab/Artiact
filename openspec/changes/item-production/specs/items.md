# Behavior

- Nonempty item goals with unique code, positive quantity/value are valid without skill milestones. Completion counts owned target inventory plus bank stock.
- Recipes reserve shared/nested ingredients globally, handle output quantities and reject cycles/invalid quantities/unsupported conditions. An unacquirable leaf is explained.
- Every tick emits at most one move, gather, required withdrawal or craft under R2 budgets and preflight. Craft/deposit/withdraw responses conserve exact quantities.
- Mock production scenario: one protected item, gather two ore, craft one bar from two ore, craft one tool from one bar. Exact final oracle: six actions (two moves, two gathers, two crafts), 32 virtual seconds, protected=1/tool=1 and zero ore/bar.
