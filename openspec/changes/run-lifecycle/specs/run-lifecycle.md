# Run lifecycle

## ADDED Requirements

### Requirement: Explicit history-preserving completion
The system SHALL archive only the exact reviewed identity of a structurally valid Completed/TargetsReached run with no unresolved intent or rejected outcome. It SHALL hold exclusive ownership during inspection and archival. Archival SHALL preserve the complete checkpoint and SHALL NOT dispatch an action.

#### Scenario: A completed run is archived
- GIVEN a completed run with verified outcomes and the matching identity digest
- WHEN the operator archives it
- THEN the active checkpoint is absent and history contains the unchanged checkpoint
- AND the next explicit Bounded invocation can initialize a new identity.

#### Scenario: Unsafe reset is refused
- GIVEN a pending, unknown, blocked, cancelled, corrupt or mismatched run, or another owner
- WHEN archival is requested
- THEN it fails without modifying the checkpoint or scheduling any action.

### Requirement: Honest durable result
The system SHALL retain initial and latest observations and the terminal time. The report SHALL include identity, target policy, counters, cooldown, elapsed time and intervention requirement. Missing historical facts SHALL remain unavailable; reconciled lost replies SHALL NOT create invented cooldown measurements.

### Requirement: Complete local bank progression
The deterministic real-client scenario SHALL reach its target after at least two deposits and conserve inventory plus bank. Completed restart and stop SHALL schedule no additional actions.

### Requirement: Process crash safety
Crashes before dispatch, after server acceptance and before response persistence SHALL recover through observation only for the pending command. Unchanged/ambiguous state SHALL prohibit repeat POST. A full process test SHALL verify this independently of session reconstruction tests.

### Requirement: Separate live acceptance
Live target attainment SHALL be recorded only from an independent read in a separately bounded rollout. Local tests SHALL NOT be described as live completion.
