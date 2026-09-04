# Artiact repository instructions

## Mission and authority

Artiact is a .NET 9 automation service for Artifacts MMO-compatible APIs. Source code and tests are authoritative for behavior; `docs/` explains the current system and must be updated when behavior, commands, configuration or contracts change.

Start with [`docs/README.md`](docs/README.md). Use [`docs/external-references.md`](docs/external-references.md) for the official game site, concepts/wiki, OpenAPI specification, Swagger UI and API operational guides. Read the nearest nested `AGENTS.md` before editing a project.

## Repository boundaries

- `Artiact/`: executable host, application services, steps and API client.
- `Artiact.Contracts/`: shared interface and data/domain contracts.
- `Artiact.MockService/`: incomplete deterministic local API substitute.
- `Artiact.Tests/`: xUnit/Moq tests.
- `Artiact/cache/`: tracked reference-data snapshots; do not refresh incidentally.
- `docker-compose.yml`: monitoring services only.

## Required workflow

- Preserve the existing C# style and avoid unrelated formatting/refactors.
- Use tests for behavior changes. Run focused tests first and `dotnet test Artiact.sln --no-restore` before completion.
- When changing interfaces or constructors, update DI in `Artiact/Program.cs`, all callers and Moq setups.
- Treat `Artiact.Contracts` changes as cross-project compatibility changes.
- Keep JSON cache updates separate from logic changes.
- Do not add secrets, `.env`, logs, `bin/`, `obj/`, certificates or generated artifacts.
- Do not run the main app merely to test compilation: its hosted worker starts game actions automatically.

## Commands

Run from the repository root:

```text
dotnet restore Artiact.sln
dotnet build Artiact.sln --no-restore
dotnet test Artiact.sln --no-restore
```

See [`docs/development.md`](docs/development.md) for focused commands and safe run instructions.

## Current high-risk seams

- Goal decomposition and recursive step construction.
- Craft-chain inventory accounting.
- Loot prerequisite planning versus live execution state.
- API retries for non-idempotent actions.
- Shared DTO serialization compatibility.
- Mock-service divergence from the real API.

Document current limitations in [`docs/known-limitations.md`](docs/known-limitations.md); do not silently broaden scope to fix them.
