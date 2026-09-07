# Design

Use a separate compose project. The mock listens on loopback by design; application and reset helper share its network namespace. Only app and observability UI ports bind host loopback. A reset helper runs before application startup. Dedicated named volume retains the app journal; the image prepares its mountpoint with non-root ownership. Collector receives OTLP/HTTP and exports Zipkin; Prometheus targets the app's shared namespace address. Add ASP.NET Core metrics instrumentation because runtime RED evidence showed no HTTP metric after health requests.
