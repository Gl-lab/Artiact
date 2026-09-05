## 1. Baseline and test boundary

- [ ] 1.1 Record clean `master` baseline build/test results and existing warnings.
- [ ] 1.2 Add the smallest isolated MockService test project/area to `Artiact.sln`; reference `Artiact.MockService` and `Artiact` only as required.
- [ ] 1.3 Build an in-memory-only host/client harness that rejects non-loopback base URIs, uses a fake/in-memory `ICacheService`, and never starts `Artiact.Program` or its hosted worker.
- [ ] 1.4 Add a guard test proving the harness does not read `.env`, user secrets, tracked cache snapshots, or create an outbound production HTTP handler.

## 2. Deterministic reset and state ownership

- [ ] 2.1 Write RED tests for explicit `basic-mining` reset, fixed virtual epoch, empty trace, unknown-scenario atomic failure, and actions before reset/character load.
- [ ] 2.2 Implement one singleton scenario store with case-insensitive character keys, deep-copied initial state, locked transitions, and content-root fixture loading.
- [ ] 2.3 Write RED tests proving first character GET initializes state and repeated GET retains coordinates, XP, inventory, time, and trace.
- [ ] 2.4 Implement retained character reads and `POST /__mock/reset` plus `GET /__mock/state/{name}` controls.

## 3. Minimal catalog compatibility

- [ ] 3.1 Write RED HTTP tests for `GET /maps`, `/resources`, `/items`, and `/monsters` page 1 contract shapes and deterministic rejection of unsupported pages.
- [ ] 3.2 Add the minimal immutable `basic-mining` catalogs and one-page controllers using existing contract DTOs.
- [ ] 3.3 Write a RED real-`GameClient.WarmUpCache()` test with empty in-memory cache and verify the four exact catalog collections are cached without disk access.
- [ ] 3.4 Make the production client compatibility test GREEN without changing tracked `Artiact/cache` snapshots.

## 4. Move, gather, virtual time, and trace

- [ ] 4.1 Write RED transition tests for deterministic move: known destination validation, atomic coordinates/time update, completed cooldown metadata, and one trace append.
- [ ] 4.2 Implement the minimal move transition through the scenario store and existing action response contract.
- [ ] 4.3 Write RED transition tests for gathering: required resource location, deterministic mining XP/item delta, atomic failure, completed virtual cooldown, and ordered trace append.
- [ ] 4.4 Implement the minimal gathering transition without randomness, real clock, delay, or background timer.
- [ ] 4.5 Write RED tests proving concurrent or failed requests cannot expose partial state, duplicate sequence numbers, or a trace entry without its state mutation.
- [ ] 4.6 Implement only the synchronization/snapshot logic required to make the atomicity tests GREEN.

## 5. End-to-end deterministic scenario

- [ ] 5.1 Write a RED in-memory HTTP test for reset -> cold catalog/character load -> move -> gather -> state/trace inspection using real `GameHttpClient` and `GameClient`.
- [ ] 5.2 Implement the minimum host seam needed for in-memory startup; do not add a production worker or outbound client.
- [ ] 5.3 Assert exact final coordinates, mining XP, inventory, virtual time, cooldowns, and the two-entry move/gather trace.
- [ ] 5.4 Reset and repeat in the same test process; compare normalized serialized state and trace for equality.

## 6. Remove network/proxy surface and document

- [ ] 6.1 Remove the YARP package and all dormant reverse-proxy/response-transform code.
- [ ] 6.2 Remove HTTPS redirection from the loopback-only mock host and keep controllers as the only runtime request handlers.
- [ ] 6.3 Add source/config scans proving no production host, external base URL, dotenv/user-secret loader, outbound `HttpClient`, proxy, or forwarder is reachable from MockService startup/tests.
- [ ] 6.4 Update `docs/mock-service.md` with exact endpoints, scenario, reset/state/trace controls, virtual cooldown semantics, unsupported surface, and local-only safety boundary.
- [ ] 6.5 Update `docs/development.md` with focused MockService test and deterministic HTTP scenario commands.

## 7. Verification and release

- [ ] 7.1 Run focused MockService tests and record exact totals.
- [ ] 7.2 Run `dotnet build Artiact.sln --no-restore` and `dotnet test Artiact.sln --no-restore`; distinguish baseline warnings from regressions.
- [ ] 7.3 Run the deterministic scenario twice and programmatically compare normalized state/trace outputs.
- [ ] 7.4 Run `git diff --check`, verify `.env` remains ignored/untracked, and scan the exact diff for secrets, production URLs, proxy/outbound-client code, generated files, and tracked cache changes.
- [ ] 7.5 Run `npx -y @fission-ai/openspec@1.12.0 validate add-minimal-deterministic-mock-service --strict` and verify all four artifacts are complete.
- [ ] 7.6 Obtain independent fail-closed review of the exact final staged diff for contract fidelity, deterministic/atomic state, trace ordering, virtual-time semantics, test isolation, absence of production networking, and unnecessary complexity; resolve every blocker through RED -> GREEN and request fresh review.
- [ ] 7.7 With release authorization, commit and push directly to `master`; verify local and remote SHA equality and a clean worktree.
