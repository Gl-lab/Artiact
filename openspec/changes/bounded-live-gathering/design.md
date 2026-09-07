# Design

An explicit RealApiBounded category uses the existing pinned authentication/inspection reads and a gather-only adapter. Fixed run directory and identity preserve ownership and budgets. Run the first durable tick, close/reopen the store to exercise recovery, run the next tick, cancel the next tick, then reopen and verify zero additional sends. This is bounded live session acceptance without hosting; HTTP stop routes remain covered by offline tests until container deployment.
