# MiddleProject

A microservices system built around an intentionally unstable external API.

Sensors at a handful of locations report air quality, motion and energy use. The system polls them
through [WeakApp](https://github.com/nantonov/WeakApp) — a deliberately unreliable upstream that
rate-limits, fails and returns corrupted bodies at random — buffers each reading through Kafka,
persists and aggregates it in PostgreSQL, and shows it on a dashboard that updates as readings
land. Everything runs from one `docker compose up -d`.

Fixed constraints explain most of the shape: GraphQL and a REST API both mandatory, a message queue
between ingestion and persistence, real-time updates over SignalR, latest .NET, and every service
independently deployable with its own pipeline and no shared assemblies.

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
| [`data_injector`](DataInjectorService) | Polls WeakApp, publishes readings to Kafka | 8082 |
| [`data_processor`](DataProcessorService) | Consumes readings, persists them to PostgreSQL | 8084 |
| [`graphql_gateway`](GraphQLGatewayService) | Serves the dashboard's GraphQL API, reading PostgreSQL directly | 8086 |
| [`notification_service`](NotificationService) | Pushes "new data, refetch" signals to browsers over SignalR | 8088 |
| [`meter_readings_ui`](MeterReadingsUI) | The dashboard: React and TypeScript over the gateway and the hub | 8090 |
| `postgres` | Reading storage | 5432 |
| `kafka` | Message queue | 9092 |
| `kafka-cluster-ui` | Kafka topic and consumer group browser | 8070 |
| `prometheus` | Metrics scraping | 9090 |
| `grafana` | Dashboards | 3000 |
| `watchtower` | Pulls newly published images and restarts the services running them | — |

Copy `.env.example` to `.env` and fill in the three API keys, then bring everything up with
`docker compose up -d`. Every service runs from a prebuilt image on Docker Hub rather than building
locally, so `up` never compiles anything — CI publishes the images and watchtower rolls them out.

Services share no assemblies. Where two of them need the same shape, each carries its own copy —
the injector and processor duplicate the Kafka message contract, the processor and notification
service duplicate the readings-persisted event, and the gateway duplicates a read-only EF mapping
over the readings tables. **DataProcessorService owns that schema** and is the
only service that migrates it; the gateway reads and never writes.

The local PostgreSQL is `meterdb` with `postgres`/`postgres` — local development credentials only:

```bash
docker compose exec postgres psql -U postgres -d meterdb -c "select sensor_type, count(*) from meter_readings group by 1;"
```

## API keys

Every business endpoint requires `X-Api-Key` and answers 401 without it; probes and `/metrics` stay
anonymous so orchestrators and Prometheus can reach them. nginx injects the key when proxying
`/graphql` and `/hubs/`, so the browser never holds one. The injector has no key: it exposes no
business API.

Keys live in `.env` (gitignored, copied from `.env.example`); the real values are repository
secrets. nginx reads them at container start, so rotating one is a restart.

| Surface | Where |
|---|---|
| Processor Swagger, the REST API | <http://localhost:8084/swagger>, key in the **Authorize** box |
| Nitro, the GraphQL IDE | <http://localhost:8090/graphql> |
| Hub monitor | <http://localhost:8090/hub-monitor/> |
| Injector and notification Swagger, probes only | <http://localhost:8082/swagger>, <http://localhost:8088/swagger> |

Nitro and the hub monitor work only through the UI's origin: a browser cannot attach a header to a
navigation or a WebSocket handshake, so nginx does it for them.

### Running your own changes

`docker-compose.yml` only pulls published images. To run the working tree, and `down` first because
the pinned `container_name` otherwise leaves the old container on the old image:

```bash
docker compose -f docker-compose.yml -f docker-compose.build.yml down && docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
```

## CI/CD and branching

Each service and the frontend has its own workflow in [`.github/workflows`](.github/workflows),
path-filtered to its own directory: restore, format, build, test with coverage, SonarQube, and on
`main` a Docker Hub push that watchtower rolls out. The gateway also re-exports its GraphQL schema
and the UI regenerates its types from it, both failing on a diff.

Work happens on `feature/*` branches and lands on `main` through pull requests.

## Observability

Services expose metrics via [OpenTelemetry](https://opentelemetry.io/), scraped by Prometheus and
visualized in Grafana. Bring the stack up with `docker compose up -d` and open:

- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (anonymous viewer access, or `admin`/`admin`)

### Correlating a reading across services

Every log line starts with the ambient trace id, or `[]` where there is none — host start-up and
anything outside a request or a poll cycle. The injector opens one span per poll cycle and writes
its trace onto each Kafka message; the processor links the batch it assembles back to the traces it
was built from and logs which ones; the notification service broadcasts under the processor's
trace. One id out of the injector's log therefore leads to the batch that ingested it and on to the
browser push.

No exporter is configured, so nothing collects the spans themselves — the ids in the logs are what
this buys.

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
