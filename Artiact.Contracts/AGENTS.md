# Contracts project instructions

This project defines shared API DTOs, domain models and the `IGameClient` boundary. Parent instructions in `../AGENTS.md` also apply.

## Compatibility rules

- Treat public members, JSON property names, nullability and collection shapes as contracts used by the main app, mock service and tests.
- Before renaming or changing a model, inspect all serializers, controllers, client methods, Moq setups and cached JSON compatibility.
- Keep transport DTOs under `Models/Api`; keep planning/domain types under `Models`.
- Do not move application behavior into contracts.
- New required properties can break deserialization and object initializers; update every caller in the same change.

## Verification

Build and test the full solution after any contract change:

```text
dotnet build ../Artiact.sln --no-restore
dotnet test ../Artiact.sln --no-restore
```
