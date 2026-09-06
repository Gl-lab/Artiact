# Disposable combat research

This .NET 9 xUnit project is intentionally outside Artiact.sln. It has no host/application reference; the Contracts reference supports actual legacy DTO deserialization probes. No runtime network, authentication, file cache, production action client or sleep is used. Restore can access NuGet. Do not promote these types into production without a separate Epic 6 change.

From the repository root:

```text
dotnet restore openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj
dotnet test openspec/changes/research-combat-equipment-progression/experiments/CombatResearch.Tests.csproj --no-restore
```

Focused filters: `FullyQualifiedName~PredictorTests`, `FullyQualifiedName~CombatRunTests`, `FullyQualifiedName~PayloadProbeTests`. Each deterministic fixture replays from fresh objects and compares independent expected observations. Generated bin/obj and test output are ignored and must not be committed.

See [protocol](../../../../docs/research/combat-equipment/experiments.md), [comparison](../../../../docs/research/combat-equipment/comparison.md) and [ADR](../../../../docs/decisions/0001-combat-viability-and-recovery.md). The prototype uses normalized single-element records, a fixed synthetic destination map 2, a preselected optional weapon and synchronous scripted responses. `reachable`, `Known` and gear conditions are explicit fixture inputs, not implementations of upstream access/condition validation. ParticipantProbe validates identity/HP only. These deliberate limits prevent mistaking the spike for a production adapter or complete simulator.
