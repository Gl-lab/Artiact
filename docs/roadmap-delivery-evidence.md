# Roadmap delivery evidence — 2026-09-07

Published R1–R5 on `codex/autonomous-roadmap`: a949e5c, 7b69fe6, 6843216, 178859c, 41895f8, 4aca343, f0668d9. Each epic has specification, implementation, tests and dated evidence in OpenSpec.

Final self-review of f0668d9 plus the RunCheckpoint/DurableRunTests diff found a reproducible ownership defect: `hero` and `HERO` could acquire separate leases. Acceptance: differently cased identities must exclude a second owner; existing checkpoints must not silently disappear after canonicalization. The fix canonicalizes ownership keys and blocks unexpected checkpoint filenames for operator migration. No independent review.

- RED: `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~FileLeaseExcludesSecondOwnerAndCheckpointRoundTrips` failed with expected missing IOException before the fix.
- Final code: `dotnet build Artiact.sln --no-restore --warnaserror` passed with zero warnings/errors.
- Final code: `dotnet test Artiact.sln --no-restore` passed: 466 application and 160 mock tests, zero failures/skips.
- Shared-contract offline gate during R4a: `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline` passed 36 tests.

## Authenticated operational evidence

The initial explicitly guarded live smoke authenticated but failed on GET `/characters/gllab` with 404. At the user's request, authenticated GET `/my/characters` returned HTTP 200 and zero characters. The user then explicitly requested creation. A single POST `/characters/create` with name `gllab` and default skin `men1` returned HTTP 200; subsequent GET `/my/characters` confirmed level 1 at (0, 0). Credentials and tokens were neither printed nor committed.

After creation, PowerShell verification passed one live test:

```powershell
try {
    $env:ARTIACT_REAL_API_READONLY='1'
    dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --filter Category=RealApiLive --logger 'console;verbosity=normal'
} finally {
    Remove-Item Env:ARTIACT_REAL_API_READONLY -ErrorAction SilentlyContinue
}
```

This confirms the dedicated reader's character/catalog smoke, not Inspect planning or gameplay correctness. No game `/action/` request was issued. Inspect, an explicitly selected first OneShot, bounded live execution and live combat remain unverified. `docker info --format '{{.ServerVersion}}'` failed because the dockerDesktopLinuxEngine pipe was unavailable; image execution, persistent storage and delivery of metrics/traces remain unverified. Branch publication is not evidence of CI success. Roadmap section 10 remains explicitly outside the first milestone. User tooling changes in `.serena/project.yml` were excluded.
