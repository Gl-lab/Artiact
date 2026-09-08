# R16: Reliable checkpoint diagnosis

Base: `231137f`. R12/R13 reopen failures and R15 transient Blocked have no established root cause. A broad session catch currently labels every unexpected exception as a checkpoint failure.

Scope: distinguish read, restore, write and execution failures using sanitized structured facts; fail closed on inaccessible state; retain budgets and pending intent across failures. Investigate the original transient without treating successful retries as a fix. Projects: Artiact, Artiact.Tests and existing MockService regression suites. No game API contract changes or live actions.

Exit requires a reproduced and diagnosed original failure, regression evidence, focused and solution gates, self-review and publication. If the historical cause remains unknown, report R16 as partial, while independently useful diagnostics may be published. R17/R18 local work may proceed with that explicit limitation; R19 cannot claim reliability acceptance.
