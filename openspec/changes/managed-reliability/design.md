# Design

Preserve the checkpoint schema and existing aggregate reason for storage compatibility. Add an optional diagnostic to decisions: operation, exception type, numeric HResult and whether a durable pending outcome needs reconciliation. Never expose exception messages, paths, identity, credentials or raw observations. Wrap only checkpoint load/save boundaries; separate restore validation from strategy execution. Store loading must distinguish absent file from a directory at the checkpoint path and an unavailable parent directory. Constructor failures release acquired ownership.

Do not retry action dispatch or blindly retry file replacement. A failed writer stops permanently in that session and cannot overwrite persisted intent on another tick. Existing process-kill tests remain the crash boundary acceptance. Historical intermittent cause remains a separate investigation item.
