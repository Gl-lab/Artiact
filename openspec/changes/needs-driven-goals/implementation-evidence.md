# R25 implementation evidence — 2026-09-08

## Reviewed scope

Base: `df5d7bf` (R24), following specification commit `e4cf63a` and the R22/R23 commits documented in implementation-plan.md. Review type: **self-review**, no independent agent review. Reviewed implementation, callers, checkpoint guards, public projection, existing command postconditions, tests, documentation and the updated architecture diagram. `Artiact.Contracts`, API action DTOs, production configuration and tracked caches are unchanged. Existing user modification `.serena/project.yml` is excluded.

Exact staged source/test diff digest at final gates: `03e088441f716f5990bb8d355e5c105545961e32`, produced in PowerShell by `git diff --cached --binary -- Artiact Artiact.Tests Artiact.MockService.Tests | git hash-object --stdin` against the base above. Documentation/evidence is outside that digest. The enclosing commit identifies the final reviewed files.

## Behavioral evidence

- Lifecycle RED: `EmptyNeedsInspectDoesNotLatchTerminalBeforeLaterNeed` returned Completed instead of Stopped before the no-needs change. Now an empty Inspect can observe a later need without a terminal latch. The newly introduced order-store tests were added together with the new class; they are not claimed as pre-implementation RED.
- Three original full-chain fixtures (`item-production`, `skill-preparation`, `resource-preparation`) complete an absolute tool order through a new factory on every tick. No general-development goal follows completion. Terminal reopen causes no new actions; consistent terminal archives, while pending or removed journal entries do not. The separate completed order survives archive.
- Cancellation after a sent action, both with a normal reply and a lost reply: original pending postcondition reconciles first; no second POST and no further preparation. Attempts are retained.
- Two-action ingredient slice: parent remains Active, archivable budget stop, next RunId uses saved physical stock; both runs total six actions and one tool. Slice evidence never calls the partial result ParentSatisfied.
- No-progress limits 1/2/40 are tested against actual Tick and per-tick restart: 1 rejects before actions, 2 and 40 complete the six-action tool chain. Restart after movement carries its spent counter; productive commands retain existing reset semantics.
- HP fixtures cover ready bank food with/without rest, production and cooking/fishing training. A fresh externally satisfied HP observation stops existing preparation without another action. Protected stock survives. The first new HP production tests exposed an estimate treating consumable supply as a skill; restricted skill-field access fixed this observed failure.
- Root stock satisfies only above its configured protected floor. Bank alternatives are generated only with permission; disabled withdrawal leaves bank ore untouched. Production shared-ingredient and bank-sibling ledger tests continue to pass in the full suite.
- Tiny budgets do not bypass a cyclic recipe, unsupported craft skill, capacity refusal, non-guaranteed yield or missing resource. Existing recipe depth/cycle and bank full/access/lost-response suites remain green.
- Required crafting, withdrawal and skill schema removal causes compatibility refusal. Initial direct schema tests got 404 because the mock scenario had not been selected; selecting the existing item-production scenario corrected the fixture, not runtime behavior.
- Order storage tests cover missing-directory read, stale revisions, corruption and durable cancellation tombstones. Operator tests cover receipt invalidation, no permission escalation, no path/credential projection, same-origin/control-header checks and HTTP 409 duplicate writes. R20 rejects Needs before any execution.
- Projection review found that many refusals could hide a selected candidate beyond the 128-entry cap. Ranking feasible candidates before truncation fixes this; `BoundedProjectionRetainsFeasibleParentBeyondFirst128Refusals` verifies the bound and retained feasible parent.

## Commands and results

All commands run from `C:\Projects\Artiact` without starting the main host:

| Command | Result |
|---|---|
| `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter FullyQualifiedName~Needs` | 20 passed at that development slice; later cases included in full gates |
| `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter 'FullyQualifiedName~NeedsBank\|FullyQualifiedName~NeedsCompatibility'` | 5 passed |
| `dotnet test Artiact.MockService.Tests/Artiact.MockService.Tests.csproj --no-restore --filter 'FullyQualifiedName~NeedsFreshSatisfied\|FullyQualifiedName~NeedsExistingRoot'` | 3 passed |
| `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter 'FullyQualifiedName~OperatorControl\|FullyQualifiedName~ScheduleTests\|FullyQualifiedName~NeedOrder'` | 33 passed |
| `dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~OperatorSnapshotTests` | 12 passed |
| `dotnet test Artiact.sln --no-restore` | 636 main tests + 331 socket-free mock tests passed, no skips |
| `node --test Artiact.Tests/operator-panel.test.cjs` | 20 passed |
| `dotnet test Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --filter Category=RealApiOffline` | 74 passed |
| `git diff --cached --check` | clean |

The escaped pipes above are Markdown table syntax; shell commands use ordinary `|` inside the quoted test filter.

Browser QA used actual embedded panel assets with a temporary localhost synthetic JSON server, not an application/game session. At a 677px screenshot width, Inspect cause/chain/full vs slice/no-progress evidence wrapped without overlap; the order form was readable, cancellation changed Active to Cancelled, reload retained cancellation. Test tab and server were closed. Node tests additionally cover order revision submission, empty-needs no-start and diagnostic values. After visual QA, the generic no-progress label was made specific and order polling was added; those text/poll changes pass the panel suite.

## Limits and final assessment

Supported subset and estimates are documented in [needs-driven-goals.md](../../../../docs/needs-driven-goals.md). Future skill thresholds are estimated from the observed maximum; official XP formulas do not turn simulation into live proof. Random/non-guaranteed production inputs are rejected, not optimistically completed. At most two production route variants and existing bounded HP alternatives are compared; arbitrary recipe-route optimization, economic valuation and all-profession training are out of scope.

No real game action, bank transaction or new-chain live acceptance was performed. R24's separately documented read-only API assessment is unchanged. OS power-loss/filesystem durability is supported by the lease/Flush/atomic replacement design and was reviewed, not tested by destructive fault injection. Independent review and live acceptance remain unverified. Within the specified offline/read-only scope, R21–R25 are implemented with their specifications, tests and documentation.
