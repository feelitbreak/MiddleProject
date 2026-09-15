# MiddleProject

A microservices system built around an intentionally unstable external API. See
[MiddleProject.md](MiddleProject.md) for the full brief.

## Services

```
WeakApp API --(HTTP poll)--> DataInjectorService --(Kafka: meter-readings)--> DataProcessorService --> PostgreSQL
                                                                                                          |
                                                                                                  (read-only queries)
                                                                                                          v
                                                            GraphQL clients <--(GraphQL: /graphql)-- GraphQLGatewayService
```

| Service | Role | Ports (host) |
|---|---|---|
| [`weak_app`](WeakApp) | The unstable external API being consumed | 8080 |
| [`data_injector`](DataInjectorService) | Polls WeakApp, publishes readings to Kafka | 8082, 8083 |
| [`data_processor`](DataProcessorService) | Consumes readings, persists them to PostgreSQL | 8084, 8085 |
| [`graphql_gateway`](GraphQLGatewayService) | Serves the dashboard's GraphQL API, reading PostgreSQL directly | 8086, 8087 |
| `postgres` | Reading storage | 5432 |
| `kafka` | Message queue | 9092 |
| `kafka-cluster-ui` | Kafka topic and consumer group browser | 8070 |
| `prometheus` | Metrics scraping | 9090 |
| `grafana` | Dashboards | 3000 |

Bring everything up with `docker compose up -d`.

Services share no assemblies. Where two of them need the same shape, each carries its own copy —
the injector and processor duplicate the Kafka message contract, and the gateway duplicates a
read-only EF mapping over the readings tables. **DataProcessorService owns that schema** and is the
only service that migrates it; the gateway reads and never writes.

The local PostgreSQL is `meterdb` with `postgres`/`postgres` — local development credentials only:

```bash
docker compose exec postgres psql -U postgres -d meterdb -c "select sensor_type, count(*) from meter_readings group by 1;"
```

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
   as done in `DataInjectorService` — see `Extensions/Extensions.cs`).
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
