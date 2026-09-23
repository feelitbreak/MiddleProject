# DataProcessorService

The Data Processor microservice of the [MiddleProject](../README.md) system. It consumes
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
- Announce each committed batch on `meter-readings-persisted`, so downstream services learn that
  readings are queryable rather than guessing at the batch linger (`ReadingsPersistedProducer`).
- Expose liveness/readiness health checks and Prometheus metrics.

## How it fits in the pipeline

```
WeakApp API --(HTTP poll)--> DataInjectorService --(Kafka: meter-readings)--> DataProcessorService --> PostgreSQL
                                                                                     |
                                                            (Kafka: meter-readings-persisted) --> NotificationService
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

### The `meter-readings-persisted` event

One event per committed batch, published by `KafkaConsumerService` after `ISender.SendAsync`
returns — that is, after `UnitOfWorkBehavior` committed, so the rows are queryable before anyone
hears about them. A consumer of `meter-readings` would instead fire while the batch was still
lingering, and the refetch it triggers would read the rows that are not there yet.

```json
{
  "publishedAt": "2026-09-16T09:31:25.481+00:00",
  "readingCount": 18,
  "newestCollectedAt": "2026-09-15T09:31:23.474862+00:00",
  "sensors": [{ "location": "Kitchen", "sensorType": "air_quality" }]
}
```

A batch that inserted nothing — a redelivery — is not announced. Delivery is at-least-once and not
transactional: an event lost to a crash between commit and publish is replaced by the next batch
within seconds, so consumers need only tolerate duplicates.

## Configuration

Settings are bound from `appsettings.json` / environment variables (`__` separator), validated via
`ValidateDataAnnotations().ValidateOnStart()` so misconfiguration fails fast at startup.

### `Kafka`

| Key | Description | Default |
|---|---|---|
| `BootstrapServers` | Comma-separated Kafka broker addresses | `localhost:9092` |
| `MeterReadingsTopic` | Topic readings are consumed from | `meter-readings` |
| `DeadLetterTopic` | Topic unprocessable messages are moved to | `meter-readings-dlq` |
| `ReadingsPersistedTopic` | Topic each committed batch is announced on | `meter-readings-persisted` |
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
| `GET /api/readings` | Readings newest first, filtered and paged |
| `GET /api/readings/latest` | The most recent reading for every sensor |
| `GET /api/readings/aggregate` | One metric aggregated into time periods, grouped by location |
| `GET /api/sensors` | The sensor catalogue |
| `GET /health/live` | Liveness — unhealthy if the consumer loop has stalled |
| `GET /health/ready` | Readiness — database reachable and partitions assigned |
| `GET /metrics` | Prometheus scraping endpoint |
| `GET /swagger` | Swagger UI (Development environment only) |

The liveness probe reports on the consumer loop's last iteration rather than merely on the process
being up: a wedged or evicted consumer leaves the web host answering requests normally while
ingesting nothing.

Both probes are mapped as ordinary handlers over `HealthCheckService` rather than with
`MapHealthChecks`. That extension writes a bare status string and registers a raw request delegate
with no method to describe, so its endpoints never reach the API explorer and never appear in
Swagger. Going through the service directly documents them alongside everything else and returns
which check failed instead of one word.

### Query API

These endpoints are **informational, for developers and operators**. The dashboard does not use
them — it reads through the GraphQL gateway, which queries the database directly. They are
therefore deliberately plain: ordinary page numbers, sensible defaults, and enums bound the way
ASP.NET Core binds them out of the box, so every call is easy to type by hand or from Swagger.

`ReadingDto` is flat rather than polymorphic: columns that do not apply to a reading's sensor type
are null and omitted from the response, so an energy reading comes back as
`{"id":…,"sensorName":"Kitchen","sensorType":"Energy","collectedAt":…,"energyKwh":12.5}`.

Note the API spells enums as their .NET names (`AirQuality`, `EnergyKwh`) while the database and
Kafka use `air_quality` and `energy`. That is not an oversight. Minimal API parameter binding uses
`Enum.TryParse` and never consults the JSON serializer, so making responses snake_case would leave
the API accepting one spelling and emitting another. The storage spelling is a separate concern,
handled in one place by `SensorTypeNames`.

#### Pagination

`?page=1&pageSize=50`, with `totalCount` and `totalPages` in the response. Ordering is by
collection time descending, then by id — the tiebreak matters because the injector stamps one
collection instant across a whole poll, so ordering by time alone would let rows shuffle between
requests and a page boundary land anywhere inside a group.

#### Aggregation — what a "period" is

`GET /api/readings/aggregate?metric=EnergyKwh` groups readings into fixed time windows and reports
statistics for each one. Only `metric` is required.

Each response row is **one interval at one location**:

```json
{ "periodStart": "2026-09-11T12:00:00+00:00", "location": "Kitchen",
  "count": 97, "average": 354.2, "minimum": 10.9, "maximum": 982.2 }
```

That row means: *between 12:00 and 13:00 UTC, the Kitchen energy meter reported 97 readings
averaging 354.2 kWh.* `periodStart` is the **start** of the window; the window ends where the next
one begins. Periods are aligned to real UTC boundaries — an hourly period starts exactly on the
hour, not an hour before whenever you happened to call.

| Parameter | Meaning | Default |
|---|---|---|
| `metric` | Which number to aggregate: `Co2`, `Pm25`, `Humidity`, `MotionDetected`, `EnergyKwh` | *(required)* |
| `interval` | Window length: `Hour`, `Day`, `Week`, `Month` | `Hour` |
| `from` / `to` | Time bounds | A window suited to the interval, ending now |
| `location` | One location, or every location | every location |

The metric also selects the sensor type, which is why one endpoint serves all three reading kinds.
`MotionDetected` maps true and false to 1 and 0, so its average is the fraction of readings in
which motion was seen.

The alignment comes from PostgreSQL's `date_trunc`. Npgsql does not expose it as a `DbFunction`, so
it is mapped with `HasDbFunction` in `MeterReadingsDbContext`, which keeps the grouping in LINQ
*and* passes the unit and time zone as bound parameters:

```sql
SELECT date_trunc(@unit, m.collected_at, @zone) AS "Key", avg(...) FROM meter_readings AS m ...
```

The time zone argument is not optional — `date_trunc` on a `timestamptz` truncates in the *session*
time zone, so omitting it would make every daily period depend on server configuration.

The range is always bounded, supplied or defaulted: at most 90 days, and at most 2000 periods per
location. The two limits guard different things — the range bounds how much is scanned, the period
count bounds how much comes back — and the period cap sits below the 2160 that 90 days at hourly
spacing would produce, so it actually constrains rather than sitting unreachable behind the range
cap.

Validation lives in the query handlers and returns `Error.CreateValidation`, which `ApiResults`
maps to a 400 problem response naming what was wrong.

## Observability

- **Logging**: Serilog, console sink, structured with source context.
- **Metrics** (`DataProcessorMetrics`, OpenTelemetry meter `DataProcessorService`):
  - `data_processor.kafka.messages_consumed`, `.batches_processed`, `.batch_retries`
  - `data_processor.kafka.dead_lettered_messages` — tagged by `reason`
  - `data_processor.kafka.readings_persisted_events_published`
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

The EF tooling defaults the environment to `Development`, so it picks up the connection string
from `appsettings.Development.json`. Point it elsewhere with
`ASPNETCORE_ENVIRONMENT` or `Database__ConnectionString`.

## Testing

```bash
cd DataProcessorService
dotnet test
```

Integration tests run against real PostgreSQL and Kafka through Testcontainers and are tagged
`[Trait("Category", "Integration")]`; Docker must be running. Filter them out with
`--filter-not-trait "Category=Integration"` when it is not.

One of them runs the database session in `America/New_York` on purpose. `date_trunc` truncates in
the session time zone, so that test is the only thing standing between a server on local time and
silently wrong period boundaries.

## CI/CD

[`.github/workflows/data-processor.yaml`](../.github/workflows/data-processor.yaml) triggers on
changes under `DataProcessorService/**`:

1. Restore, `dotnet format --verify-no-changes`, build, run tests with coverage, and analyze with
   SonarQube.
2. On push to `main`: build and push the Docker image to Docker Hub
   (`data-processor-service` tag), which `watchtower` then pulls in deployed environments.
