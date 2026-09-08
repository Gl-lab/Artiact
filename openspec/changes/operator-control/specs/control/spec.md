# Acceptance

- Inspect performs zero actions and returns a bounded expiring receipt only when Selected. Expired or unknown receipts cannot start; requested limits cannot exceed server ceilings.
- Duplicate/concurrent start clicks launch one execution. Different receipts while busy do not launch another; browser disconnect/refresh does not stop the run.
- Start still requires AllowActions and LiveActionsApproved for a non-loopback API. No operator profile enables combat.
- Restore uses the saved identity and budgets. Mismatched ID/profile/limits and an external lease owner refuse before dispatch. Terminal restart performs no new POST.
- Stop is separately requested/confirmed; an in-flight successful response is saved before further actions stop. New requests cannot reset budgets of an existing run.
- Archive delegates to existing digest/completion/unknown guards. Repeated archive does not destroy history.
- Host restart discards receipts, not durable facts. Host shutdown awaits bounded cancellation.
- Missing CSRF and cross-origin POSTs are refused. Same-origin controls work over localhost, server IP and LAN hostname; no login is required for the trusted-network deployment. Default control-off cannot start.
- Focused coordinator/HTTP tests and socket-free existing execution boundaries, solution gate, UI verification and self-review before commit/push.
