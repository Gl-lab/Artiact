# R14 verification — 2026-09-08

Reviewed base `c7e0b1f` plus R14 Operation/Strategy, two mock aliases, unit/socket-free tests and documentation. Self-review only; user's .serena/project.yml excluded. No shared DTO or new wire route.

Compiling REDs: configuration omitted CombatDiscovery before binding; both preregistered scenario resets returned404 before mock aliases existed. End-to-end tests then exposed lost parent purpose after recovery/equip; corrected retained opponent/value and original 12/78,20/120 predictions pass. A boss rejection assertion initially also excluded a legitimate dummy/ward alternative; narrowed the assertion to the unsupported guardian without changing production behavior.

- `dotnet test Artiact.MockService.Tests --no-restore --filter "FullyQualifiedName~CombatDiscovery|FullyQualifiedName~DiscoveredCombat|FullyQualifiedName~CombatRecoveryUses"`: 15 passed before three lifecycle/schema cases were added.
- `dotnet test Artiact.sln --no-restore`: 556 application +275 mock passed, zero failed/skipped. Includes repeated comparisons and process recovery from earlier epics. A final resource-charge assertion is verified in the final gate below.
- Unsupported-gear explanation test produced a compiling RED (silent filtering) and then passed after bounded explicit rejection evidence was added. `dotnet test Artiact.MockService.Tests --no-restore --filter "FullyQualifiedName~CombatDiscoveryRejects&DisplayName~effect"`: 1 passed.
- `git -c core.safecrlf=false diff --check`: passed.
- Final `dotnet test Artiact.sln --no-restore` after explanation and resource-charge assertions: 556 +275 passed, zero failed/skipped.

Review covered default/absent identity, supported effect/projection/catalog bounds, safe loot fallback, forbidden production/equipment/Fight, global craft and recovery charges, source fingerprint and pending baseline, parent provenance/combat completion, no-progress for food, schema permissions, actual archived level and original manual behavior. The observer/session/journal architecture remains; no component diagram change needed. The R12/R13 intermittent file-reopen issue remains disclosed in known-limitations; this run did not reproduce it.

Unverified: live gameplay, matched live/simulator corpus, RealApiOffline (no contract additions), R15 long-run operational matrix. No production combat readiness claim. Training/XP/cooldown and capacity estimates retain R9/R10 uncertainty.
