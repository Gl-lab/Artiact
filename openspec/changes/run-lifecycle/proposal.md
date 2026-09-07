# R6: Run lifecycle and complete gathering acceptance

## Why

Bounded sessions retain intent and budgets, but operators must currently move checkpoints manually to begin another run. Results omit the initial observation. R6 makes this transition explicit and preserves evidence without weakening unknown-outcome handling.

## Scope

1. An offline lifecycle command inspects the durable result and explicitly archives a completed, verified run under the same exclusive character lease. A subsequent Bounded invocation creates the next run; unfinished runs resume with their original identity and budgets.
2. Persist the first observation, final observed state, and completion time for a report of goals, levels/XP, inventory/bank, attempts, verified cooldown and intervention status. Older checkpoints report unavailable initial facts rather than inventing them.
3. Exercise mining to a target with at least two bank deposits through real clients and deterministic mock.
4. Exercise full host process termination around dispatch and durable response storage, proving no repeated unknown POST.
5. Document a separately scoped live gathering/bank acceptance; local delivery does not authorize game actions.

## Non-goals

Automatic repeated runs, clearing cancelled/blocked/unknown runs, distributed ownership, new game mechanics, live combat, and live bank actions without a concrete rollout scope.
