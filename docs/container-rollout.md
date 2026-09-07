# Local container rollout

`docker-compose.rollout.yml` is an isolated mock-only operations profile. It runs the production image, deterministic mock, Prometheus and OTLP Collector → Zipkin. It does not read `.env` or use live game credentials. The original `docker-compose.yml` remains the older monitoring-only example.

```powershell
docker compose -f docker-compose.rollout.yml config --quiet
docker compose -f docker-compose.rollout.yml up -d --build
Invoke-RestMethod http://127.0.0.1:5188/health/live
Invoke-RestMethod http://127.0.0.1:5188/health/ready
Invoke-RestMethod http://127.0.0.1:5188/operation
Invoke-WebRequest -Method Post http://127.0.0.1:5188/operation/stop
```

The worker uses mock researcher, mining target 10, maximum 20 actions/40 decisions/600 seconds. Its first run starts immediately after mock reset. The named journal volume is owned by non-root app UID 1654. A stopped/blocked/completed journal stays terminal. For recreation checks use `docker compose -f docker-compose.rollout.yml up -d --no-deps --force-recreate app`; do not reset the mock while checking checkpoint continuity. A fresh scenario requires deliberate review and archival of the old journal; never erase unresolved intent to retry.

App ports are shared with the mock's network namespace because the mock deliberately binds loopback. Prometheus scrapes `mock:8080`; the app exports OTLP/HTTP to `collector:4318/v1/traces`. Published ports bind only 127.0.0.1: app 5188, Prometheus 9091, Zipkin 9412. HTTP metrics include `http_server_request_duration_seconds_count`. The collector bridges the configured OTLP exporter to Zipkin's HTTP API; see [collector configuration](https://opentelemetry.io/docs/collector/configuration/).

```powershell
Invoke-RestMethod 'http://127.0.0.1:9091/api/v1/query?query=http_server_request_duration_seconds_count'
Invoke-RestMethod 'http://127.0.0.1:9412/api/v2/traces?serviceName=artiact&limit=5'
docker compose -f docker-compose.rollout.yml down
```

`down` retains the journal volume. Zipkin/Prometheus data in this acceptance profile is ephemeral. Do not treat these local image pins or this mock-only configuration as a production hosting/security/update policy. [Dated evidence](../openspec/changes/container-rollout/execution-evidence.md) records verified deployment scope.
