# Epic 8: operational hardening and staged execution

## Why
The portfolio is verified offline but host startup still starts a legacy action loop, tokens never refresh, health is static, reference cache is unversioned, and known build/advisory issues remain.

## What Changes
- Default host execution becomes inspect (zero game actions); explicit one-shot performs at most one portfolio action, with separate live-action opt-in. Legacy worker remains available only through explicit mode and action opt-in.
- Add bounded GET retries/token refresh, never repeat action POSTs, isolate Basic credentials to token requests, disable production redirects and use bounded HTTP timeouts.
- Expose API subset/version drift, observation age and operational readiness separately from liveness. Version cache storage outside tracked reference snapshots and reject corrupt/stale/mismatched entries.
- Fix known warning sources, update vulnerable Zipkin dependency to a patched compatible release, correct Docker build context and strengthen offline CI.

## Impact
Host/DI/configuration, transport/cache, shared DTO initialization (preserving wire behavior), tests, CI/container and docs. No real-character action will run in this work. Optional bank/tasks/market/multichar remain outside scope. Code and offline rollout gates can complete while live rollout remains explicitly not performed under ADR 0001.
