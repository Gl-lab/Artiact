# First approved movement — 2026-09-07

Base f6c9407. The user explicitly approved one gllab Move:277 toward mining level 2. Self-review covered exact opt-in before dotenv, origin pinning, disabled redirects, restricted GETs, exact route/body validation, persistent CreateNew intent before POST, no automatic retries and independent readback. No independent review. ReadOnlyApiVerifier's authentication/send helpers are internally reused; its read-only transport still rejects every action.

- RED `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter FullyQualifiedName~ApprovedMoveTransportTests`: 5 failed before exact-action/single-use enforcement. Temporary unused parameter warning resolved.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 51 passed, including wrong action/character/body and replay after lost response across transport instances.
- Final `dotnet test Artiact.sln --no-restore`: 478 application / 160 mock passed, zero failures/skips. `dotnet build Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --warnaserror`: zero warnings/errors.
- Explicit live command (executed once):

```powershell
try {
    $env:ARTIACT_APPROVED_ONESHOT='gllab:Move:277'
    dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --filter Category=RealApiOneShot --logger 'console;verbosity=detailed'
} finally {
    Remove-Item Env:ARTIACT_APPROVED_ONESHOT -ErrorAction SilentlyContinue
}
```

Passed 1 test. Fresh Inspect: Selected/InspectOnly, Move:277, zero attempts. Production StagedExecution OneShot: Selected/CommandVerified, Move:277, Decisions=1, Attempts=1, CooldownSeconds=10. Independent GET confirmed map_id=277, x=2, y=0. This confirms the movement and production response postcondition. Gathering, combat, bounded live execution and container telemetry were not run. The persistent ignored intent marker remains in place to prevent repeating this action; do not remove it to rerun acceptance.

No credentials, response bodies or tokens were printed or committed. User `.serena/project.yml` remains excluded.
