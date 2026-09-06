## Context

See `proposal.md` for motivation and the two capability specs for normative behavior.

The current `ActionService.Action()` owns a fixed loop of five complete goal cycles. `ArtiactBackgroundService` initializes once, retains one scope for its lifetime, repeatedly calls `Action()`, and applies one generic 30-second delay after failures. `IStep.Execute()` and step cooldown delays do not accept cancellation. `ActionService` and the gathering path in `GoalDecomposer` also treat a missing `Activity` as fatal, coupling behavior to telemetry configuration.

The repository-root `.env` exists, is ignored, and contains both generic `API_*` names and .NET `ApiSettings__*` names. The current application does not parse dotenv files. Starting the normal host against the real URL is not a smoke test because the hosted service immediately begins autonomous game actions.

OpenSpec 1.12.0 was initialized with the `spec-driven` schema. The local environment has `NODE_TLS_REJECT_UNAUTHORIZED=0`; OpenSpec commands for this change are therefore run from the already cached package with TLS validation explicitly enabled and npm offline mode. Production or real-API code MUST NOT copy or depend on the insecure process setting.

## Goals / Non-Goals

**Goals:**

- Establish one explicit orchestration cycle as the unit of policy and execution testing.
- Preserve current single-iteration behavior while removing only the hidden outer batch of five.
- Make shutdown deterministic at cycle boundaries and during orchestration-owned waits.
- Make tracing optional for behavior.
- Provide a separate, explicit, read-only real-API verification project that safely consumes `.env`.
- Keep the default solution build and test path offline and deterministic.

**Non-Goals:**

- No richer goal policy, state machine, scoring, or mining-target implementation.
- No change to the current five-cycle-equivalent continuous worker throughput guarantee; exact cadence is not promised.
- No blind real-server mutation and no action endpoint test.
- No full cancellation retrofit of every `IGameClient` HTTP method in this change.
- No MockService expansion, OpenAPI code generation, authentication redesign, or dotenv loading in production startup.
- No worker-scope lifetime redesign unless a failing characterization test proves it is required for this slice.

## Decisions

### 1. Keep the existing pipeline and expose one cycle

Replace the fixed-loop public orchestration operation with an asynchronous `ExecuteCycleAsync(CancellationToken)` that performs one goal selection, decomposition, build, and top-level step execution. Rename initialization to `InitializeAsync(CancellationToken)` for consistent semantics. The worker owns repetition explicitly.

This preserves the proven `Goal -> GoalDecomposer -> StepBuilder -> IStep` path and avoids prematurely introducing the target state-machine architecture. Alternative: add a second cycle service while leaving `Action()` intact. Rejected because it would leave two competing orchestration entry points and preserve the unsafe batch as a callable default.

### 2. Propagate cancellation through steps, but not through all HTTP contracts yet

Add a `CancellationToken` to `IStep.Execute`, `BaseStep.Delay`, and composite/conditional/action/move step execution. Every step checks cancellation before starting its next action, and every cooldown delay receives the token. Existing action delegates may still call current tokenless `IGameClient` methods. If cancellation is requested while a mutating call is in flight and it returns successfully, save its authoritative returned character snapshot first, then propagate cancellation before any delay, repeat, or following child begins. In particular, `MoveStep` must save the response before its cooldown delay rather than after it.

Alternative: add cancellation to every `IGameClient` method now. Rejected for this change because it expands a shared cross-project contract across every game operation and mock setup. The remaining limitation is explicit in the capability spec and roadmap.

### 3. Treat absent telemetry as normal

Use nullable activity calls and null-conditional status updates at every activity site reachable through the cycle, including `ActionService` and `GoalDecomposer`. Business behavior does not require a listener. Tests cover both absent-listener execution and error tagging when a listener creates an activity.

Alternative: register a mandatory listener in tests and production. Rejected because telemetry must observe execution rather than authorize it.

### 4. Keep worker recovery simple and cancellation-aware

The worker initializes once, then invokes one cycle inside its existing loop. A non-cancellation failure is logged and followed by the current 30-second token-aware delay. `OperationCanceledException` caused by the stopping token exits normally rather than being logged as critical. No new failure taxonomy is introduced yet.

Alternative: implement typed retry/replan/fatal outcomes now. Deferred to the later strategy-state-machine epic because current goals cannot yet produce those semantics cleanly.

### 5. Isolate real API verification in a project outside `Artiact.sln`

Create `Artiact.RealApiTests/Artiact.RealApiTests.csproj` as an xUnit project referenced only by explicit commands. Do not add it to `Artiact.sln`; therefore `dotnet test Artiact.sln --no-restore` remains offline. Mark the network test with trait `Category=RealApiLive`; mark all parser, validation, allowlist, redirect, and sanitization tests `Category=RealApiOffline`. Document two commands: the offline command filters `Category=RealApiOffline` and needs neither `.env` nor opt-in, while the live command filters `Category=RealApiLive` and requires `ARTIACT_REAL_API_READONLY=1`. The live fixture reads `.env` only after that guard is present. Running the live filter without the exact guard fails the live test before fixture configuration or networking.

The real project contains:

- pure dotenv parser and configuration validation tests;
- a recording-handler test proving the request allowlist;
- one live read-only smoke test that authenticates and reads the configured character plus one page of maps, resources, items, and monsters.

The documented commands are conceptually:

```text
dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --filter Category=RealApiOffline
ARTIACT_REAL_API_READONLY=1 dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --filter Category=RealApiLive
```

On Windows shells the environment-variable syntax may differ; documentation will provide the Git Bash command used by this repository's automation environment and state the invariant separately.

Alternative: put live tests in `Artiact.Tests` and filter them by trait. Rejected because a missed filter could contact the real service during the default test run. Alternative: start the normal Artiact host. Rejected because it starts the mutating background worker.

### 6. Parse dotenv without adding a package or executing shell content

Implement a small parser local to the real-test project. It recognizes only the grammar required by the spec and resolves aliases through an explicit table. It does not expand variables, process `export`, unescape shell sequences, or source the file.

Alternative: add a dotenv package. Rejected until the small required grammar proves insufficient; avoiding a dependency reduces secret-handling surface. Alternative: `source .env`. Prohibited because dotenv content must be treated as data, not commands.

### 7. Pin the credential destination

Validate the normalized URI before constructing an authorization header. Only HTTPS on `api.artifactsmmo.com` with the default port and no user information or fragment is accepted. Redirects are disabled for every verifier request, including token, character, and catalog calls. Any 3xx response fails the current operation without a follow-up request. Requests use explicit relative paths joined to the validated base URI.

Alternative: trust any configured HTTPS host. Rejected because a changed `.env` could exfiltrate credentials. Additional hosts, including a future official sandbox, require an explicit spec update.

### 8. Keep live output intentionally sparse

The smoke test reports operation names, pass/fail, response status, and non-sensitive aggregate counts only. It does not include raw response bodies, token responses, authorization headers, username, password, or full character payload. Parsing exceptions are wrapped in sanitized errors that identify only the operation and expected contract.

## Risks / Trade-offs

- [Cancellation cannot abort an already-started tokenless HTTP call] -> Save a successful authoritative response, then check cancellation immediately afterward before any wait or next action; document the limitation and handle the full client contract in a later approved change.
- [One step graph can still contain several bounded/repeated game actions] -> Do not use this cycle for real mutating smoke tests; the later atomic-command state machine remains the target architecture.
- [Changing `IStep.Execute` touches many tests and implementations] -> Apply mechanical signature propagation only after RED tests define cancellation behavior; preserve all existing step semantics.
- [Worker behavior can accidentally double-run or log normal shutdown as fatal] -> Characterize invocation counts and cancellation before modifying the worker.
- [A separate real-test project can drift because it is not in the solution] -> Document and run its build/unit tests explicitly in its own verification command. Credential-free `Category=RealApiOffline` checks run in CI; the opt-in live smoke remains outside CI and requires a separately approved secret-capable environment.
- [Official response schemas may have drifted] -> Deserialize only the selected response contracts and report incompatibility without dumping payloads; OpenAPI subset synchronization remains a separate epic.
- [Real credentials could be sent through a redirect] -> Disable automatic redirects and pin the normalized destination before authentication.
- [The host environment disables TLS validation globally for Node] -> The .NET verifier must never disable certificate validation; OpenSpec tooling uses cached offline execution with TLS validation explicitly enabled.

## Migration Plan

1. Commit OpenSpec setup and this approved change on a dedicated branch, not directly on `master`.
2. Add orchestration characterization tests and verify RED for the five-cycle behavior and telemetry coupling.
3. Implement one-cycle orchestration and worker repetition; keep existing goal behavior.
4. Add step cancellation tests, then propagate the token through step-owned waits and composites.
5. Add the isolated real-test project with pure parser/destination/request-allowlist tests.
6. Run the default solution build/tests and prove no real-server traffic occurs.
7. Only with explicit read-only authorization, run the dedicated real-API smoke command; do not start the normal host.
8. Independently review the exact diff, secret scan it, and confirm `.env` remains ignored/untracked.
9. Rollback consists of reverting the change commit; no persisted data or external schema migration exists. The read-only smoke creates no game-state rollback requirement.
