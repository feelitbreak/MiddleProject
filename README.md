# MiddleProject

A microservices system built around an intentionally unstable external API.

Sensors at a handful of locations report air quality, motion and energy use. The system polls them
through [WeakApp](https://github.com/nantonov/WeakApp) — a deliberately unreliable upstream that
rate-limits, fails and returns corrupted bodies at random — buffers each reading through Kafka,
persists and aggregates it in PostgreSQL, and shows it on a dashboard that updates as readings
land. Everything runs from one `docker compose up -d`.

The interesting problem is not the data, which is synthetic. It is keeping a pipeline honest when
the thing feeding it is not: retrying without amplifying, staying idempotent under redelivery, and
never showing a number that stopped being true.

## Why it is shaped this way

The architecture answers a set of fixed constraints, which is worth knowing before asking why a
piece exists:

- **GraphQL is mandatory**, and so is a **REST API** — hence a GraphQL gateway serving the
  dashboard and a plain REST query API on the processor for operators.
- **A message queue is mandatory** as the buffer between ingestion and persistence. Kafka, so the
  processor can be slow or absent without the injector noticing.
- **Real-time updates** to the browser, over SignalR.
- **Each service is independently deployable**, with its own CI pipeline and Docker image, and
  **shares no assemblies with any other**. Where two services need the same shape, each carries its
  own copy — a duplicated contract is cheaper than a coupled build.
- Latest .NET, data persisted to a database, structured logging, and unit plus integration tests.

## Services

```
WeakApp API --(HTTP poll)--> DataInjectorService --(Kafka: meter-readings)--> DataProcessorService --> PostgreSQL
                                                                                    |                     |
                                                                 (Kafka: meter-readings-persisted) (read-only queries)
                                                                                    v                     v
                              MeterReadingsUI <--(SignalR: /hubs/readings)-- NotificationService   GraphQLGatewayService
                                          |                                                               ^
                                          +---------------------------(GraphQL: /graphql) refetch---------+
```

| Service | Role | Ports (host) |
|---|---|---|
| [`weak_app`](WeakApp) | The unstable external API being consumed | 8080 |
| [`data_injector`](DataInjectorService) | Polls WeakApp, publishes readings to Kafka | 8082, 8083 |
| [`data_processor`](DataProcessorService) | Consumes readings, persists them to PostgreSQL | 8084, 8085 |
| [`graphql_gateway`](GraphQLGatewayService) | Serves the dashboard's GraphQL API, reading PostgreSQL directly | 8086, 8087 |
| [`notification_service`](NotificationService) | Pushes "new data, refetch" signals to browsers over SignalR | 8088, 8089 |
| [`meter_readings_ui`](MeterReadingsUI) | The dashboard: React and TypeScript over the gateway and the hub | 8090 |
| `postgres` | Reading storage | 5432 |
| `kafka` | Message queue | 9092 |
| `kafka-cluster-ui` | Kafka topic and consumer group browser | 8070 |
| `prometheus` | Metrics scraping | 9090 |
| `grafana` | Dashboards | 3000 |
| `watchtower` | Pulls newly published images and restarts the services running them | — |

Bring everything up with `docker compose up -d`. Every service runs from a prebuilt image on Docker
Hub rather than building locally, so `up` never compiles anything — CI publishes the images and
watchtower rolls them out.

Services share no assemblies. Where two of them need the same shape, each carries its own copy —
the injector and processor duplicate the Kafka message contract, the processor and notification
service duplicate the readings-persisted event, and the gateway duplicates a read-only EF mapping
over the readings tables. **DataProcessorService owns that schema** and is the
only service that migrates it; the gateway reads and never writes.

The local PostgreSQL is `meterdb` with `postgres`/`postgres` — local development credentials only:

```bash
docker compose exec postgres psql -U postgres -d meterdb -c "select sensor_type, count(*) from meter_readings group by 1;"
```

## CI/CD and branching

Each service and the frontend has its own workflow in [`.github/workflows`](.github/workflows),
triggered only by changes under its own directory. Every one restores, checks formatting, builds,
tests with coverage and runs a SonarQube analysis; on `main` it builds a Docker image and pushes it
to Docker Hub, where watchtower picks it up. Two of them carry an extra gate: the gateway re-exports
its GraphQL schema and fails on a diff, and the UI regenerates its types from that schema and does
the same — so a schema change that nobody regenerated against breaks the build rather than the
dashboard.

Work happens on `feature/*` branches and lands on `main` through pull requests.

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
