# R23 evidence — 2026-09-08

Base `9384ec2`; self-review of this commit's bounded maximum-drop bank simulation, discovery integration, optional identity/evidence fields, tests and docs. Existing BankPrerequisite, dispatch, journal and reconciliation were reviewed and left unchanged. No independent review or live acceptance claimed.

- RED: `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~BankPolicyCannotMakeProtectedOutputFit`: 1 failed because a protected-output path incorrectly had a command.
- `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~AutonomousGoalDiscoveryTests`: 31 passed. Independent expected costs: two gathers plus two moves and one deposit = 5 actions / 27 estimated seconds before multiplier; six gathers/two unloads from resource map = 12 actions / 64 seconds. Original stocks remain unchanged.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter 'FullyQualifiedName~Bank|FullyQualifiedName~Deposit'`: 18 passed, including exact conservation, no-remediation and lost-reply reconciliation.
- `node --test Artiact.Tests/operator-panel.test.cjs`: 17 passed.
- `dotnet test Artiact.sln --no-restore`: 629 application + 303 MockService tests passed; final panel embedded-resource rebuild included.
- `git diff --check`: passed; LF/CRLF notices only.

Limits: simulation is an estimate of future XP/yields, bounded to 10,000 gathers/unloads; bank can change externally and live preflight remains authoritative. Main app and real bank actions were not run. No new Contracts or mock mechanic was required. Architecture topology is unchanged; gathering-bank/operator documentation now describes the estimator.
