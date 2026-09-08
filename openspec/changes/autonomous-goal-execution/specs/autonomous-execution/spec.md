# Requirements

## Requirement: One budget across dynamic milestones
Bounded autonomous execution SHALL complete at least two discovered milestones without manual goals and SHALL retain counters, active selection, history and algorithm version across reconstruction. Completed local goals SHALL trigger discovery without reporting the character fully developed.

## Requirement: Resolve pending first
A saved pending command SHALL be reconstructed against its original goal and baseline. An unknown outcome SHALL NOT cause another POST or a newly selected goal. Incompatible versions and failed checkpoint persistence SHALL block.

## Requirement: Explicit terminal lifecycle
Budget and observed supported-cap exhaustion SHALL terminate with distinct Stopped reasons. Restart SHALL return the saved terminal without observing or acting. run-result SHALL show history, unfinished goal and charges. Archive SHALL preserve exact bytes, require a verified journal and valid final observation, and leave unsafe checkpoints unchanged. A new run SHALL require a new RunId after explicit archive.

## Requirement: Quality gate
The accumulated preregistered scenarios SHALL meet useful outcomes, stock/budget invariants, zero-regression ceilings and at least one independent useful-result/command-count win. A failed gate SHALL keep R12 open, retaining all losses in evidence.
