# Container evidence — 2026-09-07

Base b2c96c0. Docker Desktop 28.0.1. Self-review covered separate mock-only compose, non-root volume ownership, loopback ports, reset ordering, collector route, metric registration and exclusion of local run data from build context. No independent review.

RED: image built before this change with `docker build -f Artiact/Dockerfile -t artiact:roadmap .`; running it on loopback 5188 and requesting /health/live returned 200, but /metrics contained six bytes and no `http_server_request_duration` sample. Added ASP.NET Core metric instrumentation. The initial probe container was stopped and removed.

- `docker compose -f docker-compose.rollout.yml config --quiet` passed.
- `docker compose -f docker-compose.rollout.yml up -d --build` built both images and started mock, reset, app, collector, Prometheus, Zipkin. Reset exited successfully.
- `GET http://127.0.0.1:5188/health/live` returned 200 Alive; `/health/ready` returned ready=true, Bounded:Selected:CommandVerified, API 8.2.3.
- `POST http://127.0.0.1:5188/operation/stop` returned 202. Final status Cancelled, attempts=2, cumulative cooldown=12; mock trace contained move then gathering.
- Prometheus `GET /api/v1/query?query=http_server_request_duration_seconds_count` at localhost:9091 returned samples for health/live, health/ready, operation and POST operation/stop, including status 202. This is receiver-side evidence, not just an exporter endpoint.
- Zipkin localhost:9412 `/api/v2/services` returned artiact; `/api/v2/traces?serviceName=artiact&limit=5` returned five traces. Collector used pinned 0.123.0 image and explicit OTLP/HTTP → Zipkin pipeline.
- `docker compose -f docker-compose.rollout.yml exec -T app id` reported UID/GID 1654. `/var/lib/artiact` held an app-owned JSON checkpoint and lock file.
- Before app recreation, mock trace count=2 (move, gathering). `docker compose -f docker-compose.rollout.yml up -d --no-deps --force-recreate app` retained the journal volume. After recreation, status remained Cancelled, decisions=2, attempts=2, cooldown=12 and mock trace still contained exactly two actions. Process-local stopRequested resets to false; terminal cancellation remains persisted.
- `dotnet test Artiact.sln --no-restore`: 478 application / 160 mock passed, zero failures/skips.
- `dotnet build Artiact.RealApiTests/Artiact.RealApiTests.csproj --no-restore --warnaserror`: zero warnings/errors.

Scope: local mock container hosting, real health/status/stop HTTP, persistence across app recreation and actual receiver-side metrics/traces. Live game execution is separately documented in the first-move/bounded-gathering evidence. No production remote deployment, sustained load test, complete game compatibility or combat live readiness is claimed.
