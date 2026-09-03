# MiddleProject

## Observability

Services expose metrics via [OpenTelemetry](https://opentelemetry.io/), scraped by Prometheus and
visualized in Grafana. Bring the stack up with `docker compose up -d` and open:

- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (anonymous viewer access, or `admin`/`admin`)

### Pattern for adding a new service

Each service exposes its own metrics — there's no shared library across them, since services in
this repo may be written in different languages.

1. **Expose `/metrics`** on the service's normal HTTP port, in Prometheus exposition format (for
   .NET services: `OpenTelemetry.Exporter.Prometheus.AspNetCore` + `MapPrometheusScrapingEndpoint()`,
   as done in `DataInjectorService` — see `Extensions/ObservabilityExtensions.cs`).
2. **Metric naming**: use dotted OpenTelemetry instrument names,
   `<service>.<subsystem>.<noun>` (e.g. `data_injector.kafka.messages_produced`). The Prometheus
   exporter automatically converts these to snake_case with a unit suffix
   (`data_injector_kafka_messages_produced_total`). Rely on standard ASP.NET Core / HTTP client /
   runtime instrumentation for request counts, durations, and process/GC stats — add custom
   instruments only for business-specific metrics not already covered.
3. **Scrape config**: add one job block to [`prometheus.yml`](prometheus.yml) pointing at
   `<container_name>:<port>`, `/metrics`.
4. **Dashboard**: drop a Grafana dashboard JSON into `grafana/dashboards/` — it's auto-provisioned,
   no manual import needed.