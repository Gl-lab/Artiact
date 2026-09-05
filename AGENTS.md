# Artiact repository instructions

## Mission and authority

Artiact is a .NET 9 automation service for Artifacts MMO-compatible APIs. Source code and tests are authoritative for behavior; `docs/` explains the current system and must be updated when behavior, commands, configuration or contracts change.

Start with [`docs/README.md`](docs/README.md). Use [`docs/external-references.md`](docs/external-references.md) for the official game site, concepts/wiki, OpenAPI specification, Swagger UI and API operational guides. Read the nearest nested `AGENTS.md` before editing a project.

## Repository boundaries

- `Artiact/`: executable host, application services, steps and API client.
- `Artiact.Contracts/`: shared interface and data/domain contracts.
- `Artiact.MockService/`: incomplete deterministic local API substitute.
- `Artiact.Tests/`: xUnit/Moq tests.
- `Artiact.MockService.Tests/`: socket-free deterministic scenario and real-client compatibility tests.
- `Artiact.RealApiTests/`: separate offline boundary checks and opt-in live smoke; excluded from the solution.
- `Artiact/cache/`: tracked reference-data snapshots; do not refresh incidentally.
- `docker-compose.yml`: monitoring services only.

## Required workflow

- Follow the proportional change and completion workflow in [`docs/development.md`](docs/development.md#change-workflow). Use existing OpenSpec artifacts for planned changes; small fixes do not need a new specification ceremony.
- Before implementation, identify observable acceptance criteria, affected projects and relevant failure cases. Keep each reviewable slice buildable, with its behavior tests and documentation together.
- Preserve the existing C# style and avoid unrelated formatting/refactors.
- Use tests for behavior changes. Run focused tests first and `dotnet test Artiact.sln --no-restore` before completion.
- When changing interfaces or constructors, update DI in `Artiact/Program.cs`, all callers and Moq setups.
- Treat `Artiact.Contracts` changes as cross-project compatibility changes.
- Keep JSON cache updates separate from logic changes.
- Record exact verification commands, results, reviewed revision/diff and unverified scope in the change evidence or final handoff; `[verified]` alone is not evidence.
- Check affected diagrams, nested instructions and known limitations against the final code. Store changing test totals in dated evidence rather than duplicating them across guides.
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
