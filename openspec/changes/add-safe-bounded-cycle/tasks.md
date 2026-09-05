## 1. Baseline and isolation

- [x] 1.1 Create a dedicated implementation branch from verified `master` after approval; verify `git status --short --branch` is clean except for the approved OpenSpec files and `.env` is ignored/untracked.
- [x] 1.2 Run `dotnet build Artiact.sln --no-restore` and `dotnet test Artiact.sln --no-restore`; record the existing warning count and verify the 21-test baseline before production edits.
- [x] 1.3 Record the default solution project/test list before creating the real-API project; verify it contains no real-server fixture and that merely having `.env` produces zero network activity.

## 2. One-cycle orchestration

- [x] 2.1 Write focused `ActionService` tests proving one invocation selects, decomposes, builds, and executes exactly one goal; run the focused tests and verify RED because no single-cycle operation exists.
- [x] 2.2 Write focused initialization-cancellation tests proving a pre-cancelled token starts neither cache warm-up nor character loading, and cancellation after warm-up prevents character loading; run them and verify RED.
- [x] 2.3 Replace the multi-cycle operation with `InitializeAsync(CancellationToken)` and `ExecuteCycleAsync(CancellationToken)` in `IActionService`/`ActionService`; run the focused tests and verify GREEN without changing one-iteration goal/step semantics.
- [x] 2.4 Write focused tests proving cycle execution, including gathering decomposition, succeeds when all reachable `ActivitySource.StartActivity` calls return null; run them and verify RED from the current `Listener not initialized` failures.
- [x] 2.5 Make activity creation observational and nullable in `ActionService` and `GoalDecomposer` while retaining error status when an activity exists; run telemetry-focused and orchestration tests and verify GREEN.

## 3. Worker repetition and cancellation

- [x] 3.1 Write worker tests proving initialization occurs once, each loop invokes one cycle, cancellation starts no new cycle, recoverable failure waits once, and cancellation during recovery exits without a critical error; run them and verify expected RED behavior.
- [x] 3.2 Update `ArtiactBackgroundService` to call the explicit one-cycle operation, pass its stopping token, preserve the current bounded recovery delay, and treat stopping-token cancellation as normal shutdown; run worker tests and verify GREEN.
- [x] 3.3 Write step tests proving cancellation before execution starts no action and cancellation during a cooldown wait starts no repeat or following child; run them and verify RED because `IStep.Execute` currently has no token.
- [x] 3.4 Write a regression test proving that when cancellation occurs during an already-started tokenless mutating `IGameClient` call, its successful returned character snapshot must be saved before cancellation exits and no cooldown wait, repeat, or following child starts; run it and verify RED from the current tokenless execution/save ordering.
- [x] 3.5 Propagate `CancellationToken` through `IStep`, `BaseStep`, `ActionStep`, `MoveStep`, `GatheringStep`, `ConditionalStep`, `MixedStep`, and all call sites; save every successful authoritative action response before observing cancellation, pass the token to step-owned delays, and check it before each next action; run all step cancellation and reconciliation tests and verify GREEN.
- [x] 3.6 Document the in-flight cancellation limitation in `docs/known-limitations.md` and verify the docs agree with the passing reconciliation tests.

## 4. Safe dotenv configuration

- [x] 4.1 Create `Artiact.RealApiTests/Artiact.RealApiTests.csproj` without adding it to `Artiact.sln`, add the recording-handler fixture, and verify the default solution test list and project graph do not include it.
- [x] 4.2 Write dotenv parser tests for comments, blank lines, first-equals separation, matching quotes, literal shell syntax, malformed lines, duplicate keys, aliases, and conflicting aliases; run the dedicated focused tests and verify RED before parser implementation.
- [x] 4.3 Implement the minimum non-executing dotenv parser and deterministic alias resolver; run parser tests and verify GREEN without adding a dotenv package.
- [x] 4.4 Write destination-validation tests for official HTTPS success and rejection of HTTP, foreign hosts, user information, fragments, and non-default ports; run them and verify RED.
- [x] 4.5 Implement fail-closed destination validation before authorization-header construction and disable automatic redirects for every verifier request; add a 3xx test for token, character, and catalog operations and verify no follow-up request occurs.

## 5. Read-only real API verifier

- [x] 5.1 Write recording-handler tests proving the request allowlist contains only `POST /token` followed by GET character/maps/resources/items/monsters requests and rejects every `/action/` path; run them and verify RED before verifier implementation.
- [x] 5.2 Implement the verifier client and sanitized result types; run allowlist tests and verify GREEN with no username, password, token, authorization header, raw response body, or full character payload in captured output or exceptions.
- [x] 5.3 Write tests proving the live-category test fails explicitly with zero network requests without `ARTIACT_REAL_API_READONLY=1`, while the offline-category command passes without opt-in or credentials; also cover missing/malformed/conflicting configuration and sanitized authentication/contract failures; run and verify RED.
- [x] 5.4 Implement the separate `RealApiOffline` and `RealApiLive` categories, exact live opt-in guard, repository-root `.env` loading after the guard, token authentication, selected read requests, contract parsing, and non-sensitive aggregate summary; run the offline filter and the live filter without opt-in, verifying offline GREEN and explicit live non-success with zero network requests.
- [x] 5.5 Document the exact offline and live category commands, read-only boundary, destination pin, and prohibition on starting the normal autonomous host for smoke verification; inspect docs and verify no secret examples or real values are present.
- [ ] 5.6 Only after separate explicit read-only authorization, run the dedicated real-API smoke once; verify token acquisition plus character and one-page catalog reads succeed, record only operation/status/count evidence, and confirm no `/action/` request occurred.
  - Current verification blocker: the approved live run and a credential-free `curl` probe both timed out connecting to `api.artifactsmmo.com`; authentication did not complete, so no character, catalog, or action request was sent.

## 6. Full verification and review

- [x] 6.1 Run `dotnet build Artiact.sln --no-restore` and compare warnings with baseline; verify no new warning is hidden among existing warnings.
- [x] 6.2 Run `dotnet test Artiact.sln --no-restore`; verify all existing and new default-suite tests pass and no real-server request occurs.
- [x] 6.3 Run the dedicated `Category=RealApiOffline` filter with no `.env` dependency or live opt-in; verify parser, destination, allowlist, redirect, and sanitization tests pass offline, then run `Category=RealApiLive` without opt-in and verify explicit non-success with zero requests.
- [x] 6.4 Run `git diff --check`, inspect `git status --short --ignored`, and scan the exact diff for secret values; verify `.env` remains ignored/untracked and no credentials, token, authorization header, or response payload was added.
- [x] 6.5 Run `openspec validate add-safe-bounded-cycle --strict` and `openspec status --change add-safe-bounded-cycle`; verify the change remains structurally valid and implementation tasks match delivered behavior.
- [x] 6.6 Obtain an independent fail-closed review of the exact final diff for spec compliance, cancellation behavior, accidental real-network access, secret exposure, and unnecessary complexity; convert every rejection into a failing test and repeat review after fixes.
- [ ] 6.7 After verification and separate commit/push authorization, commit the bounded-cycle change without `.env` or generated output, push the implementation branch, and verify the remote commit hash before reporting completion.
