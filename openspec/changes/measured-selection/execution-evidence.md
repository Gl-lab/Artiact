# R5 evidence — 2026-09-07

Base 4aca343. Self-reviewed default regression behavior, multi-candidate full-world fingerprint rebasing, sample-only-on-verified path, persisted samples/incumbent, bounded ratios and action/travel/recovery components. No independent review.

- RED `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~MeasuredSelectionTests`: three failed comparisons before measurement integration (fixed totals 48/36/80 instead of 21/15/35). Temporary unused-parameter warning resolved during implementation.
- Focused `dotnet test Artiact.sln --no-restore --filter "FullyQualifiedName~MeasuredSelectionTests|FullyQualifiedName~MeasuredResourceAlternatives"`: 5 application and 1 mock passed, including restart/hysteresis and full-world preflight/reply.
- `dotnet test Artiact.sln --no-restore`: 466 application / 160 mock passed, zero failures/skips.

Comparisons use independently specified cooldowns and final expected totals, identical four-action goal completion and deterministic replay. See docs/measured-selection.md for scope: no global optimum, no forced exploration, assumed unknown prerequisites and no live performance/calibration claim. R3/R4 socket-free flows remain regression evidence.

External operational status: Docker CLI is installed, but `docker info --format '{{.ServerVersion}}'` reports unavailable dockerDesktopLinuxEngine pipe; no daemon/build/container run verified. Host/game actions, authenticated live reading and telemetry delivery not run. CI workflow runs master pushes/PRs; branch pushes alone are not CI evidence.
