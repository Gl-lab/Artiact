# Container acceptance

The local profile shall run the production application as non-root with a persistent journal volume and a deterministic loopback mock. It shall not load real credentials. Startup shall wait for explicit mock reset. Health/status/stop shall be accessible on host loopback only.

Prometheus shall receive HTTP duration samples after health requests. OTLP traces shall reach Zipkin through a collector. Stopping the bounded worker and recreating only the app container shall preserve terminal status, counters and the mock action count. Startup with an old journal shall not silently start a new run.
