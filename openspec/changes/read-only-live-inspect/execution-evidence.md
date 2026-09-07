# Evidence — 2026-09-07

Base 2ec7768. Self-review: opt-in before dotenv loading, pinned origin, redirects disabled, GET allowlist, POST rejection, production observer/probe/planner reuse and sanitized decision. No host/cache writes or independent review.

- RED `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --filter FullyQualifiedName~InspectionTransportTests`: 6 forbidden-route assertions failed before allowlist, 4 allowed-route assertions passed. Temporary unused parameter warning resolved.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`: 46 passed.
- `dotnet test Artiact.sln --no-restore`: 466 application / 160 mock passed.
- With `ARTIACT_REAL_API_READONLY=1`, `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --filter Category=RealApiInspect --logger 'console;verbosity=detailed'`: live acceptance failed with Blocked/NoFeasibleCandidate, NoSupportedResource, Attempts=0. Public copper_rocks data has guaranteed ore plus five optional drops; gathering currently requires exactly one drop. This is inspection evidence, not an actionable rollout gate. Follow-up implementation is required.

Automatic command review rejected the initial host launch without a specific reason; it did not run. The isolated verifier is a narrower read-only implementation. Docker daemon, OneShot and deployed telemetry remain unverified.
