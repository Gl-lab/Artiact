# Design

Separate RealApiOneShot category and exact opt-in string. Reuse pinned authentication, no redirects and restricted inspection GETs. Wrap with a one-use move transport that validates the complete body and atomically creates a persistent marker before sending. A marker blocks future invocations even if the first outcome is uncertain. Run the production OneShot service without a web host; report its decision and sanitized location afterward. The marker is ignored local operational state, not a secret or tracked artifact.
