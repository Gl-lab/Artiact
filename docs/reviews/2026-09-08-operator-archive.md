# Operator archive dead end — 2026-09-08

Base: `ae51dee`, reviewed local diff in RunCheckpoint, OperatorCoordinator, OperatorSnapshotReader, panel HTML/JS, their tests, operator guide and R18 artifacts. Self-review; unrelated `.serena/project.yml` changes untouched.

User evidence: screenshots show Blocked/NoFeasibleCandidate, an unavailable archive action, and misleading accepted/continue guidance. A durable terminal cannot execute new work. The fix permits exact-byte archival of that specific known failure when shared journal consistency, finished time, version, identity, pending and result guards pass. It does not turn failure into success. Other failures and unresolved actions remain protected. CLI archive uses the same predicate. Snapshot CanArchive projects the write guard, and terminal Start returns RunFinished. UI explains unavailable archival, disables terminal continuation, directs eligible outcomes to archive/new session, and replaces accepted-start text when a terminal snapshot arrives.

Acceptance evidence:

- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~RunLifecycleTests`: behavioral RED was three failures archiving NoFeasibleCandidate (v1/v2 and zero actions). A preceding test syntax error was fixed and is not counted as RED. Four unsafe variants were already GREEN and remained protected.
- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter "FullyQualifiedName~RunLifecycleTests|FullyQualifiedName~Operator"`: final 41 passed. Later coordinator/projection regression tests were GREEN when added after implementation; they prove terminal refusal and consistent snapshot eligibility, not RED history.
- `node --test Artiact.Tests/operator-panel.test.cjs`: initial added UI tests exposed missing archive explanations and contradictory mismatch guidance. Final 14 passed, including archive eligibility, no terminal dispatch and accepted-message replacement.
- `dotnet test Artiact.sln --no-restore`: final 609 application + 301 mock/compatibility tests passed; no failures or skips.
- `git diff --check`: passed.

Reviewed failure cases: pending commands, unverified journal, inconsistent counts, missing finish, preserved outcome bytes, new identity after archive, finished start without executor dispatch. Public HTTP additions: Run.CanArchive and refusal RunFinished. Existing APIs/DI signatures and diagrams are otherwise unchanged. No game actions, checkpoint edits, deployment, or tests against the user's live run were performed. New session planning can still produce NoFeasibleCandidate; diagnosing that planning result is separate from the archive/continuation defect. No new browser visual QA was performed for the explanatory text; actual script behavior is covered by Node tests.
