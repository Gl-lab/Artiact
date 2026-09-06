# Mock service instructions

This project is a deterministic, incomplete substitute for selected Artifacts MMO endpoints. It must not silently be described as a complete emulator or production proxy. Parent instructions in `../AGENTS.md` also apply.

Read `../docs/mock-service.md` before changes.

## Change rules

- Match `GameClient` routes and contract DTOs exactly for every implemented endpoint.
- Reset with `POST /__mock/reset` and `{ "scenario": "basic-mining" }` or `{ "scenario": "mining-progression" }`, then load `GET /characters/MockHero` once before action calls. Repeated character reads preserve mutations.
- Keep state process-local unless persistence is an explicit requirement.
- Keep the two normative fixtures in `BasicMiningScenario.json` and `MiningProgressionScenario.json`; preserve the original basic fixture byte-for-byte; preserve atomic state/trace transitions, deep snapshots and virtual cooldown semantics. Do not introduce production forwarding.
- Verify behavior through `Artiact.MockService.Tests` and its TestServer compatibility suite. Keep expected contract values independent of the fixture being tested; assert inventory ordering explicitly where contractual.
- Fixed tokens and development data are not authentication evidence.
- Add tests before relying on a new endpoint for an end-to-end main-app check.
- Update `docs/mock-service.md` whenever supported endpoints or deliberate divergences change.

## Run

```text
dotnet run --project Artiact.MockService.csproj --launch-profile http
```

The HTTP profile listens on `http://localhost:5000`.
