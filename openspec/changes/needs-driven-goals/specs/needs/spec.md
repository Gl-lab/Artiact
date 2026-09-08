# Observable acceptance

Implement every R25.4 roadmap scenario, with separate tests for order-store atomicity/revisions/tombstones, no-needs Inspect followed by a new need, terminal reopen and archive, corrupt/pending archive refusal, and schedule rejection before reservation.

Prove that complete inventory stops further preparation, shared/protected stock is conserved, cancelled parents do not return, HP recovered externally stops supply after pending reconciliation, and final budget exhaustion does not claim parent completion.

Test full chain forbidden/inaccessible/working-capacity failures with apparently feasible early slices. Test no-progress below/exactly/above boundaries against real Tick, spent counters and restart. Include a multi-run ingredient or skill slice followed by fresh replan for the same absolute order. API/lost reply boundaries reuse the durable action journal and never retry blindly.

Operator/Inspect evidence shows source → parent result → missing ingredient → minimum required skill, chosen alternative, full-chain versus slice estimates and observed remaining work. Full solution, panel, offline API boundary and dedicated socket-free chain acceptance are required. Live new chains require separate concrete approval and evidence.
