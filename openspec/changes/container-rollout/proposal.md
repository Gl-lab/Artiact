# Local container operations acceptance

Complete roadmap section 9 in Docker Desktop using mock-only credentials: production application image, dedicated persistent journal volume, HTTP health/status/stop, Prometheus scrape and OTLP-to-Zipkin trace delivery. Do not deploy live credentials or game automation in this environment.

Acceptance: bounded worker status visible over HTTP; stop survives application container recreation without extra mock actions; non-root process writes journal volume; Prometheus receives HTTP request duration samples; Zipkin returns application traces. Existing monitoring-only compose is preserved.
