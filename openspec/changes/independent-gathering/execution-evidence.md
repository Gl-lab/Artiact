# R1 evidence — 2026-09-07

Base: 59b69a68c414b92c98a024219b46b3ec8416479c. Reviewed change: independent-gathering application, tests, authored mock schema and documentation diff; unrelated `.serena/project.yml` excluded.

- RED: `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~MiningOnlyIgnoresCombatModelAndCombatCatalogs`: 2 failed on rejected configuration before implementation. Later the same two failed on referenced MapLayer before the primitive-reference fix.
- Focused: `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter "FullyQualifiedName~StagedOperationTests|FullyQualifiedName~StrategyPortfolioFlowTests"`: 31 passed before additional negative coverage.
- Full: `dotnet test Artiact.sln --no-restore`: 441 application and 144 mock tests passed, zero failures/skips.
- Self-review: optional policy, catalog selection, preflight, response stock/state, negative input, schema references and docs checked. No independent review. Whitespace check performed before commit.

Official public [OpenAPI](https://api.artifactsmmo.com/openapi.json) was read on this date (version 8.2.3): GET character/maps/resources, POST move with map_id and gathering, integer profession/capacity fields and referenced MapLayer match the selected boundary. Gathering exposes cooldown, in-progress, low-level and inventory-full rejections. [Skills concepts](https://docs.artifactsmmo.com/concepts/skills) informs profession separation. This comparison is a subset check; not every schema constraint/mechanic is implemented.

Not verified: authenticated real-character reading/actions, Docker, telemetry delivery, separate RealApiOffline and combat experiments (their code/DTOs unchanged). No host or live game action was run. Publication recorded separately in git history.
