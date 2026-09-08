# R20: finite scheduling and local notifications

Base b4e1d23. R16 is accepted with its documented limitation; the scoped R19 trial is complete. Implement default-off finite autonomous gathering series through shared StagedExecution, with per-run ceilings, durable aggregate reservations, end date and maximum run count. Local panel notifications are the delivery channel; no external messages, credentials or new gameplay are authorized by development.

Acceptance: restart, duplicate ticks/event delivery, busy character, unknown/corrupt state, exhausted aggregate budget and stop/disable cannot cause extra dispatch. Finish only verified runs through the existing archive rules; preserve every unsafe checkpoint. Notifications appear only on run completion, series completion/stop or intervention, with stable IDs and bounded retained history. Projects: Artiact, Artiact.Tests and MockService.Tests; no Contracts changes.
