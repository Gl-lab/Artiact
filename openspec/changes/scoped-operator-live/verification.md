# Scoped operator preparation — 2026-09-08

Base `0d7ad75`; reviewed scope is the five new Scoped* files in RealApiTests, this OpenSpec change and linked protocol/development/roadmap docs. Self-review only. No production source/contracts/defaults changed. Existing `.serena/project.yml` excluded. R16 remains open; no game-action approval inferred from continuation.

TDD commands:

- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter FullyQualifiedName~ScopedAutonomousTransportTests`: compiled forwarding scaffold,10 failures (forbidden dispatches were sent, no observation/stop/latch checks); implementation then10 passed. Additional freshness/world,16-action cap and catalog-reuse/seal cases pass in final gate.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter FullyQualifiedName~ScopedOperatorHostTests`:3 guard tests passed, real HTTP startup test failed because scaffold returned404 for controls. Registered real panel/coordinator; final gate passes, no automatic execution and unauthenticated Start403.
- `dotnet test Artiact.sln --no-restore`:576 application +288 mock tests passed.
- `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline`:73 passed on final code, including socket loopback host startup; no production requests or credentials. No build/test warnings in final gates; transient unused-field warnings belonged to the RED scaffold only.
- `git diff --check`: passed; routine LF-to-CRLF Git notices only.

Review found and resolved two pre-release risks: checkpoint filenames are identity hashes rather than checkpoint.json, so the harness now refuses any existing trial-directory contents; replay is now preceded by explicit transport sealing and proof of a terminal checkpoint without pending intent. Ambient host configuration sources are cleared and Kestrel binds explicitly to IPv4 loopback.

Live read-only refresh: temporarily set ARTIACT_REAL_API_READONLY=1 in PowerShell try/finally and restored its previous value; `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-build --no-restore --filter Category=RealApiBudgetAssessment --logger 'console;verbosity=detailed'`:1 passed at2026-09-08T13:05:57.1896816Z,43 GETs, acquisition15.7315483 seconds. Character fingerprint `13D88836A152ACD94031164B9167CBAA879D42C3A80AE5E9128C41D54CA02488` and world `5B26B5CE443FE734ADBE9AD87E2B35D8534CB3235991B7686F4C3D21AD16C273` match the transport pins. Map277,free98,copper_ore2 unchanged. Matrix again selected Move:379 for alchemy2 at13/134,16/180,16/600, with zero attempts in every row. The900-second proposal retains headroom with this sample too:36*15.732+164 approximately730.4 seconds. This command did not exercise the live action host. The gameplay category RealApiOperatorLive has not been invoked. No host marker/run directory created for the real trial.

Unverified: combined live operator cycle, real action response/cooldown/XP, scope-triggered stop through the actual game client, durable live reopen and R16 historical transient. Boundary tests and existing production suites do not establish these facts. The retained checkpoint and strict post-send failure latch prohibit automatic replay; user scope approval must include the open R16 prerequisite.
