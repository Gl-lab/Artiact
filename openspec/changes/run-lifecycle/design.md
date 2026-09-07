# Design

Keep the version-1 checkpoint readable by adding optional fields. Persist the first successful observation once, independently from the last command baseline. Persist the last observation and terminal timestamp; distinguish observed from command-verified state. Reports expose local state only through an explicit offline command, before host construction and configuration/secrets loading.

Use `dotnet Artiact.dll run-result <directory> <origin/character>` to read the active checkpoint. Use `run-archive <directory> <origin/character> <expected-identity-sha256>` to approve precisely the reported identity. Commands acquire the existing lease and never instantiate clients. Archiving requires Completed/TargetsReached, no pending intent, consistent counters and only verified/reconciled journal outcomes. Move the checkpoint atomically to a history subdirectory with a collision-resistant name. A crash leaves either the active checkpoint or the complete archive; never delete first. The archive command cannot create a run or dispatch a game action. Changing settings on an active checkpoint remains incompatible.

History files are outside the root checkpoint discovery pattern. Archive preserves exact bytes; failure preserves the source. A completed session remains terminal even if stop is requested or the host restarts. Cancelled/blocked sessions remain operator intervention cases and cannot be reset with this command.

Testing separates file lifecycle/report contracts, session persistence, socket-free real-client bank acceptance, and full-process fault injection. Process tests must use isolated loopback mock state, temporary run/cache directories and sentinel credentials, with bounded timeouts. Never use the configured production host as a compilation probe.
