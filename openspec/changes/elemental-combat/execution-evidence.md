# R4b evidence — 2026-09-07

Base 178859c. Self-reviewed arithmetic channels, raw presence/domain checks, existing fire-only equipment mutation, ADR and docs; no independent review.

`dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~ElementalCombatTests`: three RED failures before channel computation (six instead of two exchanges, ignored secondary damage and negative secondary attack accepted). After implementation independent mixed/rounding/invalid cases pass.

`dotnet test Artiact.sln --no-restore`: 461 application and 156 mock passed, zero failures/skips. The old rejection case for air attack=1 was intentionally replaced by upper-bound rejection and explicit mixed-channel acceptance. Missing secondary fields and negative resistance remain rejected. Historical isolated research code unchanged; its separate suite not rerun.

Official [combat guide](https://docs.artifactsmmo.com/concepts/stats_and_fights/) inspected before implementation. No live fight, authenticated simulation or matched observation corpus. Only the documented independent elemental family is added; fire-channel-only weapon replacement remains, broader gear/effects/defeat recovery are not implied.
