# Mock service instructions

This project is a deterministic, incomplete substitute for selected Artifacts MMO endpoints. It must not silently be described as a complete emulator or production proxy. Parent instructions in `../AGENTS.md` also apply.

Read `../docs/mock-service.md` before changes.

## Change rules

- Match `GameClient` routes and contract DTOs exactly for every implemented endpoint.
- Seed a character through `GET /characters/{name}` before action calls.
- Keep state process-local unless persistence is an explicit requirement.
- Prefer deterministic scenarios in `MockData.json`; never forward requests to the real API without a separate approved design.
- Fixed tokens and development data are not authentication evidence.
- Add tests before relying on a new endpoint for an end-to-end main-app check.
- Update `docs/mock-service.md` whenever supported endpoints or deliberate divergences change.

## Run

```text
dotnet run --project Artiact.MockService.csproj --launch-profile http
```

The HTTP profile listens on `http://localhost:5000`.
