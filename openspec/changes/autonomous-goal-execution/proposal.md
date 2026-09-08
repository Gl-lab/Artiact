# R12: Durable autonomous gathering

## Why
R11 can explain automatically discovered milestones but cannot execute them. Rebuilding independent sessions would refund limits and lose unknown-command identity.

## Scope
Enable autonomous Bounded execution in the existing session and checkpoint store. Persist selected discovery evidence, completed/rejected transitions and version independently of stable user policy. Keep an active goal until completion or confirmed infeasibility. Reconcile pending commands before discovering anything new. Add explicit terminal lifecycle reasons and guarded archive support. Run the accumulated quality protocol through real clients and test restart boundaries.

## Non-goals
OneShot/Legacy autonomous modes, healing/combat discovery, live game actions and continuous hosting remain excluded.
