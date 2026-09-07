# Skill prerequisites

## ADDED Requirements

### Requirement: Parent-bound preparation
An enabled item goal SHALL explain its missing skill and a bounded training prerequisite. The prerequisite SHALL keep the parent candidate identity and share its session budget. Completed parent goals SHALL schedule no further training.

### Requirement: Crafting training
The system SHALL select a supported accessible recipe whose skill requirement is met and whose item level is in the nonzero-XP range. It SHALL produce a bounded additional batch, acquire its ingredients and verify actual skill progress. Training output SHALL be retained. A non-progressing reply SHALL NOT permit an endless training loop.

#### Scenario: Skill then final item
- GIVEN insufficient weaponcrafting for a tool and an accessible ore-based training recipe
- WHEN the item goal runs
- THEN it gathers training materials, trains, obtains remaining materials and produces the tool without configuration changes.

### Requirement: Gathering training
When a required resource is above the current supported gathering skill, the system SHALL train on an eligible lower resource, then return to the required resource after its level is reached.

### Requirement: Bounded failure
Missing recipes, skills, access or XP facts, material ceilings and cyclic prerequisites SHALL yield explicit rejection. Rejected training SHALL preserve state. Restart SHALL preserve the parent's attempts and pending outcome; loss of a training response SHALL trigger read-only reconciliation, not a second craft.

### Requirement: Compatibility
Preparation SHALL be opt-in and part of the policy identity. Existing production profiles SHALL preserve their existing behavior when disabled.
