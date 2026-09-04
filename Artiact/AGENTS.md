# Main application instructions

This directory contains the executable ASP.NET Core host and autonomous background worker. Parent instructions in `../AGENTS.md` also apply.

## Read before changing

- Runtime and dependencies: `../docs/architecture.md`
- Domain and looting flow: `../docs/domain-model.md`
- Configuration and commands: `../docs/development.md`

## Change rules

- `Program.cs` is the DI composition root. Constructor/interface changes must be reflected there and in tests.
- `ActionService` orchestrates; domain planning belongs in the focused service/resolver, not in HTTP transport code.
- Steps must update `CharacterService` from the API response and respect cooldown semantics.
- Recursive craft planning must remain cycle-safe and must not invent reusable real inventory.
- Loot execution predicates must use live character state and remain bounded.
- New `IGameClient` actions require contract, client, step, mock/test-double and test impact review.
- Never log credentials, Basic headers, Bearer tokens or user-secret values.

## Verification

Run the narrow test class for the changed service, then:

```text
dotnet test ../Artiact.sln --no-restore
```

Running this project starts real background actions unless it is explicitly configured against a safe compatible service.
