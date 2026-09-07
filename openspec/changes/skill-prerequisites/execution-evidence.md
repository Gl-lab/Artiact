# R7 evidence — 2026-09-07

Base/reviewed diff: `0d2a62c` plus the skill-prerequisites wrapper, policy/configuration, profession API probe, authored mock additions, tests and guides. Self-review covered recursion, stock conservation, unknown outcomes, disabled-policy identity and scope. No independent review is claimed.

`skill-preparation` reaches weaponcrafting 2/XP1 and tool=1/bar=1/protected=1 through exactly nine commands and 50 virtual seconds. `resource-preparation` trains mining before rare-ore access and finishes tool=1/ore=2/protected=1 through seven commands and 40 seconds. Tests assert literal command sequences and inventory values, terminal reconstruction, lost training-reply reconciliation, one shared budget, material ceiling, cyclic/deep dependencies, inaccessible required resource, schema absence and zero-XP rejection. The mock's unmet-skill rejection preserves state and trace.

## Verification

- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~MissingCraftSkillSelects`: RED before planner edits, failed the missing prerequisite explanation assertion; after implementation 1 passed. The optional policy data shape was introduced before this test so compilation was not the claimed RED.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~ItemGoalTrains|FullyQualifiedName~TrainingReplyLoss"`: 3 passed.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~Training|FullyQualifiedName~ItemGoalTrains|FullyQualifiedName~MissingCraftSkill"`: 7 passed at that slice.
- `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~CyclicOrTooDeepTraining`: 2 passed.
- `dotnet test Artiact.sln --no-restore`: final 499 application and 176 mock/process tests passed, no failures/skips.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 54 passed.
- `git diff --check`: passed with LF/CRLF notices.

An earlier full run exposed a remaining R6 Windows harness collision when reading a recovering checkpoint while the child replaced it. The test now awaits the worker's terminal event before inspecting the restored file. Focused process tests (3) and the final solution passed after that fix. No production storage retry or weaker safety check was introduced.

Public sources read without authorization: official skills page (workshop/skill and zero-XP eligibility) and OpenAPI mining/weaponcrafting progress fields and CraftSchema skill reference. No game actions, authenticated reads, live training, containers, research suite or Linux/remote CI acceptance were performed. Contracts DTOs were not changed. Synthetic XP and the two selected professions do not establish support for every profession. See docs/skill-preparation.md for capacity, standalone crafting milestones and equipment-wrapper exclusions.
