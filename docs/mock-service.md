# Artiact.MockService

## Purpose

`Artiact.MockService` is a standalone ASP.NET Core service that provides a deterministic local substitute for a small subset of the Artifacts MMO API. It supports development of character loading, movement, gathering and crafting without changing a real character.

It is not a complete API emulator. The project name says `MockService`, while its root namespace and namespaces remain `Artiact.SmartProxy` from an earlier reverse-proxy design.

## Running

From the repository root:

```text
dotnet run --project Artiact.MockService/Artiact.MockService.csproj --launch-profile http
```

The `http` launch profile listens on `http://localhost:5000`, which matches `Artiact/appsettings.dev.json`. Set the main app environment to `Dev` for that file name to be loaded by `Program.cs`.

The mock host calls `UseHttpsRedirection`; verify requests are not redirected unexpectedly when using the HTTP-only profile.

## Implemented endpoints

| Method and route | Behavior |
|---|---|
| `POST /token` | Returns a fixed placeholder token; it does not validate Basic credentials |
| `GET /characters/{name}` | Loads matching character from `MockCharacters.json`; falls back to `NewCharacter`; seeds the in-memory cache under the requested name |
| `POST /my/{name}/action/move` | Updates cached coordinates from `MoveRequest` |
| `POST /my/{name}/action/gathering` | Applies a matching `gathering` scenario at the character's current coordinates |
| `POST /my/{name}/action/crafting` | Applies a matching `crafting` scenario for coordinates and item code, multiplied by requested quantity |

Action responses contain the updated character and an empty `Cooldown` object.

## Data and state

- `MockCharacters.json` supplies initial character snapshots.
- `MockData.json` supplies position/action/target scenarios and inventory or skill-XP changes.
- `CharacterCache` is a singleton dictionary. State is process-local and lost on restart.
- A character is not available to action endpoints until `GET /characters/{name}` has initialized it.
- JSON files are loaded from the service content root or current working directory as implemented; run through `dotnet run --project` to preserve the expected layout.

## Unsupported main-client actions

The main `IGameClient` supports more operations than the mock service. These routes are currently absent:

- fight;
- rest;
- equip and unequip;
- use item;
- recycling;
- delete item;
- paginated maps, resources, items and monsters endpoints.

Because `/action/fight` is absent, the looting-aware craft scenario cannot be exercised end-to-end against `MockService` without extending it. Reference data may still come from fresh JSON cache files; an expired/missing cache would make the main app request endpoints the mock does not implement.

## Known implementation caveats

- Swagger services are registered, but Swagger middleware/endpoints are not enabled in `Program.cs`; the HTTPS launch profile's `swagger` launch URL is therefore misleading.
- YARP is referenced by the project, but all reverse-proxy registration and mapping is commented out.
- Exceptions are used for expected invalid states and are not mapped to stable game-compatible error responses.
- Crafting rejects non-positive quantity with a generic exception.
- Inventory removal can reduce an item below zero before removing it; availability validation is commented out.
- The XP expressions use `changes.Xp.Difference ?? 0 * multiplier`; due operator precedence, a non-null XP difference is not multiplied for multi-item crafting.
- `CharacterExtension` level rollover subtracts the threshold from the incoming XP difference instead of accumulated XP and handles only one level, so existing XP can be lost or become negative.
- Character cache keys are case-sensitive, mutable and unsynchronized. A repeated character GET reloads the fixture and overwrites accumulated mock state.
- `MockData.json` uses a relative process-working-directory path, unlike `MockCharacters.json`, which uses `ContentRootPath`.
- The fixed token is test data only and must never be treated as authentication evidence.

## Safe extension checklist

When adding a mock endpoint:

1. Match the route, request DTO and response DTO used by `GameClient`.
2. Apply state changes through `ICharacterCache`.
3. Add controller/service tests or an integration test before relying on it for a main-app smoke test.
4. Keep behavior deterministic; encode scenarios in mock data rather than calling the real API.
5. Document any deliberate difference from the real API.
