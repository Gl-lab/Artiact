## 1. Source and contract research

- [x] 1.1 Read applicable instructions and current combat/client/DTO tests at the execution revision; record baseline and research scope.
- [x] 1.2 Create the dated contract matrix for fight/rest/equip/unequip and required observation/movement routes; capture OpenAPI version/hash, schema pointers and exact local gaps.
- [x] 1.3 Document mechanics, defeat, HP/recovery, damage/rounding/randomness, effects, XP/drop and equipment rules; record contradictions and supported-subset blockers.
- [x] 1.4 Assess official simulation access and calibration evidence availability without authenticated calls; distinguish documented capabilities from tested behavior.

## 2. Experiment design

- [x] 2.1 Define named starting snapshots, target, supported opponent/equipment subset and independent expected safe/unsafe/boundary results before coding.
- [x] 2.2 Define candidate goal semantics, terminal precedence, risk hypothesis and positive decision/fight/recovery/no-progress bounds including counter reset rules.
- [x] 2.3 Specify authored current-schema payload examples and expected local deserialization gaps, including fight participant selection and equipment slots.

## 3. Disposable offline prototypes

- [x] 3.1 Create an isolated .NET 9 xUnit experiment project under this change's experiments directory, outside the solution/host; add an in-memory recording action port and virtual cooldown seam.
- [x] 3.2 Write and run predictor behavior tests first; implement the narrow candidate model and record actual RED/GREEN or already-GREEN outcomes.
- [x] 3.3 Write and run finite milestone, rest, defeat and equipment transition tests first; implement only the prototype needed to compare the hypotheses.
- [x] 3.4 Cover every required experiment group in design.md, including invalid limits, turn exhaustion, cancellation, ambiguous response loss with no replay, and loops without XP progress.
- [x] 3.5 Execute each deterministic fixture twice and compare independent decisions, counts, state and virtual time; record schema compatibility probes separately from model correctness.

## 4. Decision and Epic 6 boundary

- [x] 4.1 Write the alternatives comparison with evidence, uncertainty, false-safe cases and maintenance cost; do not treat finite sampled wins as a guarantee.
- [x] 4.2 Write the ADR selecting the supported viability/recovery model or a justified no-go; resolve LevelUpGoal semantics, unknown outcomes, risk criterion and emulator scope.
- [x] 4.3 Produce the Epic 6 handoff: starting state/target, minimum mock/API subset, DTO/client/retry/access prerequisites, acceptance scenarios and unresolved operator decisions. Do not implement Epic 6.

## 5. Verification and handoff

- [x] 5.1 Run focused offline experiment tests and deterministic replay; record exact commands/results and reviewed revision/diff identity.
- [x] 5.2 Run `dotnet test Artiact.sln --no-restore`, strict change/all OpenSpec validation and whitespace checks on final files; record warnings and unverified scope.
- [x] 5.3 Self-review requirements against evidence and all failure paths; reproduce blockers before fixes and rerun affected gates. State review type accurately.
- [x] 5.4 Link completed research/ADR from docs/README.md; check affected docs/diagrams/limitations without claiming unchanged production behavior is fixed. Record final go/no-go and remaining blockers.

## Completion status — 2026-09-06

Research and offline experiments are complete. The ADR recommends a bounded deterministic Epic 6 slice and records a no-go for live combat pending downstream prerequisites. Exact commands, observed RED/GREEN, coverage, review scope and limitations are recorded in [execution-evidence.md](execution-evidence.md). [verification.md](verification.md) remains historical planning evidence. No predecessor archival or production combat implementation is included.
