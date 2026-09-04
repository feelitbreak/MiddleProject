# DataProcessorService

The Data Processor microservice of the [MiddleProject](../MiddleProject.md) system. It consumes
meter readings from Kafka, published by [DataInjectorService](../DataInjectorService), and persists
them into PostgreSQL.

## Responsibilities

- Consume the `meter-readings` Kafka topic in batches, committing offsets only once a batch is
  durably stored (`KafkaConsumerService`).
- Decode each message's polymorphic `payload` based on its `type` discriminator
  (`MeterReadingMessageDecoder`), resolving it into a strongly-typed reading.
- Resolve each reading's sensor — the upstream API has no sensor identifier, so location plus kind
  is the natural key — to a surrogate key, creating the sensor on first sight (`SensorRegistry`).
- Persist a whole batch in one idempotent statement, so redelivery is a no-op
  (`MeterReadingRepository`).
- Move messages that can never be processed to `meter-readings-dlq` with failure-reason headers,
  rather than blocking the partition (`DeadLetterProducer`).
- Expose liveness/readiness health checks and Prometheus metrics.

## How it fits in the pipeline

```
WeakApp API --(HTTP poll)--> DataInjectorService --(Kafka: meter-readings)--> DataProcessorService --> PostgreSQL
```

## Architecture

Clean Architecture, with dependencies pointing inwards only:

| Project | Contents |
|---|---|
| `src/Domain` | Entities, the sensor type vocabulary, and the `Result`/`Error` primitives. No dependencies. |
| `src/Application` | CQRS abstractions, the dispatcher, pipeline behaviours, and use-case handlers. Depends on Domain. |
| `src/Infrastructure` | EF Core persistence, the Kafka consumer and dead-letter producer, telemetry. Depends on Application. |
| `src/Api` | Thin ASP.NET Core host: composition root, health checks, metrics endpoint. Depends on Infrastructure. |

### CQRS without MediatR

MediatR moved to a commercial licence at 13.0.0, so the dispatcher is hand-rolled: `ICommand`,
`IQuery`, their handlers, `IPipelineBehavior` and `ISender` in
`Application/Abstractions/Messaging`, with the dispatcher in `Application/Dispatch`.

Handlers are registered **explicitly** in `Extensions.AddCqrsHandlers` rather than discovered by
assembly scanning. Scanning needs `MakeGenericType`, which raises trimming warnings that fail the
build under `TreatWarningsAsErrors`, and only reports a forgotten interface at runtime. Instead,
`RequestExecutor<TRequest, TResult>` is constructed at the registration call site, where every type
argument is a compile-time generic parameter, and stored in a frozen dictionary keyed by request
type. Dispatch is a lookup, a cast and one interface call — no runtime reflection.

Pipeline behaviours are open generics, so their own constraints decide what they apply to:
`UnitOfWorkBehavior` is constrained to `IBaseCommand` and is therefore never constructed for a
query.

### Database

Code-first, applied through EF Core migrations. Two tables:

- `sensors` — the catalogue, unique on `(name, sensor_type)`.
- `meter_readings` — table-per-hierarchy, one table for all three reading kinds with a
  `sensor_type` discriminator and disjoint nullable value columns.

`collected_at` is stamped by the injector at poll time, since the upstream API supplies no
timestamp. It is normalised to UTC and truncated to microseconds before storage (`UtcInstant`),
because `timestamptz` rejects a non-zero offset outright and stores microseconds — without
truncation an in-memory deduplication key would disagree with the unique index.

**Idempotency.** `ux_meter_readings_sensor_collected` is unique on `(sensor_id, collected_at)`, and
ingestion inserts with `ON CONFLICT DO NOTHING`. Kafka delivers at least once, and a crash between
the database commit and the offset commit redelivers a batch that is already stored; skipping
collisions makes that a no-op. Note this deduplicates *messages*, not *source data* — two polls of
an unchanged upstream value carry different `collected_at` stamps and are legitimately two rows.

**Why one raw statement.** EF Core has no insert-from-select and no upsert API, so conflict-skipping
insertion cannot be expressed in LINQ. `MeterReadingRepository` is the only hand-written SQL in the
service; every other query, including the aggregations, is LINQ. Values are passed as arrays
expanded by `unnest`, so the statement takes a fixed parameter count regardless of batch size. The
statement is a constant built from the column constants in `MeterReadingColumns` — no caller input
reaches it, values arrive only as typed parameter bindings, and a contract test asserts the
constants still match the EF model.

## Configuration

Settings are bound from `appsettings.json` / environment variables (`__` separator), validated via
`ValidateDataAnnotations().ValidateOnStart()` so misconfiguration fails fast at startup.

### `Kafka`

| Key | Description | Default |
|---|---|---|
| `BootstrapServers` | Comma-separated Kafka broker addresses | `localhost:9092` |
| `MeterReadingsTopic` | Topic readings are consumed from | `meter-readings` |
| `DeadLetterTopic` | Topic unprocessable messages are moved to | `meter-readings-dlq` |
| `ConsumerGroupId` | Consumer group identifier | `data-processor` |
| `MaxBatchSize` | Messages accumulated before a batch is written | `500` |
| `BatchLingerMs` | How long to wait for a partial batch to fill | `2000` |
| `MaxBatchAttempts` | Attempts before a transiently-failing batch is dead-lettered | `5` |
| `RetryBaseDelayMs` | Initial backoff between batch attempts | `500` |
| `RetryMaxDelayMs` | Ceiling on the exponential backoff | `30000` |
| `MaxPollIntervalMs` | Longest gap between polls before the broker evicts this consumer | `300000` |
| `SessionTimeoutMs` | Consumer session timeout | `45000` |

### `Database`

| Key | Description | Default |
|---|---|---|
| `ConnectionString` | Npgsql connection string | *(required)* |
| `CommandTimeoutSeconds` | Per-command timeout | `30` |
| `ApplyMigrationsOnStartup` | Apply pending migrations before serving | `false` |
| `MaxRetryCount` | Retries for a transient connection failure | `5` |
| `MaxRetryDelaySeconds` | Ceiling on the connection retry delay | `10` |

Pin `Options=-c timezone=UTC` in the connection string. PostgreSQL's `date_trunc` truncates in the
*session* time zone, so a container on local time would silently bucket aggregates to local
midnight.

In `docker-compose.yml` (repo root) this service runs as `data_processor`, pointed at
`Kafka__BootstrapServers=kafka:29092` and the `postgres` service.

## Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Liveness — unhealthy if the consumer loop has stalled |
| `GET /health/ready` | Readiness — database reachable and partitions assigned |
| `GET /metrics` | Prometheus scraping endpoint |
| `GET /swagger` | Swagger UI (Development environment only) |

The liveness probe reports on the consumer loop's last iteration rather than merely on the process
being up: a wedged or evicted consumer leaves the web host answering requests normally while
ingesting nothing.

The REST query API (paged readings, latest-per-sensor, time-bucketed aggregates) is not in this
change; it follows next.

## Observability

- **Logging**: Serilog, console sink, structured with source context.
- **Metrics** (`DataProcessorMetrics`, OpenTelemetry meter `DataProcessorService`):
  - `data_processor.kafka.messages_consumed`, `.batches_processed`, `.batch_retries`
  - `data_processor.kafka.dead_lettered_messages` — tagged by `reason`
  - `data_processor.database.readings_inserted` / `.readings_duplicates_skipped`
  - `data_processor.kafka.batch_duration`, `.consumer_lag`
  - `data_processor.database.write_duration`
  - Plus ASP.NET Core and .NET runtime instrumentation.

Dashboard: `grafana/dashboards/data-processor-overview.json`.

## Running locally

Via the full stack from the repository root:

```bash
docker compose up -d
```

Or standalone against an already-running Kafka and PostgreSQL:

```bash
cd DataProcessorService
dotnet run --project src/Api
```

### Migrations

```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations
```

A design-time factory supplies the context, so this needs no reachable database. For commands that
do talk to one, such as `dotnet ef database update`, set
`DATAPROCESSOR_DESIGNTIME_CONNECTION`.

## Testing

```bash
cd DataProcessorService
dotnet test
```

## CI/CD

[`.github/workflows/data-processor.yaml`](../.github/workflows/data-processor.yaml) triggers on
changes under `DataProcessorService/**`:

1. Restore, `dotnet format --verify-no-changes`, build, run tests with coverage, and analyze with
   SonarQube.
2. On push to `main`: build and push the Docker image to Docker Hub
   (`data-processor-service` tag), which `watchtower` then pulls in deployed environments.
