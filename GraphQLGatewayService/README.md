# GraphQLGatewayService

The read API the dashboard talks to. It serves a strongly-typed GraphQL schema over the readings
that [DataProcessorService](../DataProcessorService) has persisted, querying PostgreSQL directly
rather than proxying that service's REST endpoints. See [the repository README](../README.md) for
how the whole system fits together.

## Responsibilities

- Expose readings, sensors and aggregations through one GraphQL endpoint (`GraphQL/Queries/`).
- Filter, paginate and aggregate in the database rather than in the client
  (`Infrastructure/Persistence/Queries/`).
- Keep the endpoint bounded: page-size caps, execution depth, cost limits (`Extensions.cs`).
- Report errors without leaking internals (`GraphQL/Errors/GatewayErrorFilter.cs`).
- Expose health probes and Prometheus metrics.

It consumes no Kafka, writes nothing, and owns no DB schema.

## How it fits in the pipeline

```
WeakApp API --(HTTP poll)--> DataInjectorService --(Kafka)--> DataProcessorService --> PostgreSQL
                                                                                          |
                                                                                  (read-only queries)
                                                                                          v
                                            GraphQL clients <--(GraphQL: /graphql)-- GraphQLGatewayService
```

## Architecture

| Project | Holds |
|---|---|
| `src/Domain` | The service's vocabulary: `Result`/`Error`, the sensor and metric enums, the GraphQL-facing contracts, and `AggregateWindow`'s range rules. No persistence types. |
| `src/Infrastructure` | The read-only EF model, its mapping, the composable queries, and the meter. |
| `src/Api` | The host, the GraphQL schema and resolvers, health endpoints. |

### Why there is no Application layer

DataProcessorService has one because it has commands: an ingestion pipeline, a unit of work, and
behaviours wrapped around them. This service only reads. A CQRS dispatcher between a resolver and
an `IQueryable` would be indirection with exactly one caller, which the repository's "no
speculative abstractions" rule rules out.

## Schema ownership

**DataProcessorService owns the `sensors` and `meter_readings` tables and is the only service that
migrates them.** This service has no `Migrations` folder, never calls `Migrate()`, and has no
`ApplyMigrationsOnStartup` switch to turn on.

Because services in this repository share no assemblies, the EF mapping is duplicated here rather
than referenced. The duplicates are deliberate and must not drift:

- `src/Infrastructure/Persistence/ReadModels/{MeterReadingRow,SensorRow}.cs`
- `src/Infrastructure/Persistence/Configurations/*.cs`
- `src/Infrastructure/Persistence/MeterReadingColumns.cs`
- `src/Domain/Enums/{SensorType,SensorTypeNames}.cs`

They carry a `Row` suffix on purpose: they are the shape of a row this service reads, not entities
that define what a reading *is*. A column rename in DataProcessorService is a breaking change here,
and nothing in these files may change without a corresponding migration there.

## GraphQL API

The endpoint is `POST /graphql`. The committed [`src/Api/schema.graphql`](src/Api/schema.graphql)
is the contract; CI re-exports it and fails if it drifts from the code.

XML doc summaries are compiled into the schema as descriptions, so the Nitro IDE and any
introspection-driven tooling document themselves. Keep those summaries phrased for an API consumer
rather than a C# caller — no "Gets the ...", which reads wrong in a schema.

| Query | Returns |
|---|---|
| `readings(where, first, after, last, before)` | A cursor connection of readings, newest first, with `totalCount` |
| `latestReadings(where)` | The most recent reading from each matching sensor |
| `sensors` | The sensor catalogue |
| `locations` | Every distinct location, for a filter control |
| `readingAggregates(metric, interval, where)` | One time-bucketed series per location |
| `health` | The same checks the probes run |

### Querying from React

The schema is shaped so one filter object drives the whole page. `readings` and `readingAggregates`
both take a `where:` argument, so the client holds a single filter in state and spreads it into one
document; everything except `metric` is optional, so the first render shows data before the user
touches a control.

**Pagination is cursor-based, and the intended UI is "load more", not numbered pages.** Readings
arrive continuously, so with offsets the rows inserted after page one was fetched shift the window
and page two repeats what the caller already has. Cursors are keyset predicates over
`(collectedAt, id)`, so that cannot happen. Use `totalCount` for a "N readings match" label,
`pageInfo.endCursor` with `fetchMore`, and Apollo's built-in `relayStylePagination()` field policy
to merge pages — no custom cache code. The connection exposes `nodes` as well as `edges`, so list
rendering is `data.readings.nodes.map(...)`.

`readingAggregates` returns data already grouped into one series per location, each carrying its
`unit`, so a chart maps straight onto it: one line per series, no client-side `groupBy`, and no
hard-coded unit lookup to drift from the metric enum.

### Guard rails

A public GraphQL endpoint is trivially overloaded, so these are on in every environment: page size
capped at 100 (25 by default), execution depth at 12, operation cost at 5000, a 15-second
execution timeout, and aggregations bounded to 90 days and 2000 periods. The cost ceiling is
calibrated against the schema: a fully expanded `readingAggregates` analyses at 2010, so the
limit admits any single query while rejecting the same aggregation aliased three times over. Introspection and the Nitro IDE are
Development-only; `GET` is refused everywhere.

Errors always reach the client with a message and an `extensions.code`
(`BAD_USER_INPUT`, `NOT_FOUND`, `SERVICE_UNAVAILABLE`, `INTERNAL_SERVER_ERROR`) and are always
logged. Stack traces and raw exception text are **never** returned, in any environment — a
development instance is still reachable, and an Npgsql exception message can carry connection
details. An unexpected failure returns a correlation id that also appears in the log line.

## Configuration

### `Database`

| Key | Description | Default |
|---|---|---|
| `ConnectionString` | Npgsql connection string. Required. | — |
| `CommandTimeoutSeconds` | Per-command timeout. | `30` |
| `MaxRetryCount` | Retries for a transient connection failure. | `5` |
| `MaxRetryDelaySeconds` | Ceiling on the retry delay. | `10` |

The connection string **must** pin `Options=-c timezone=UTC`. `date_trunc` truncates in the session
time zone, so without it aggregation periods would follow the server's local midnight.

### `Cors`

| Key | Description | Default |
|---|---|---|
| `AllowLocalhost` | Allow any `localhost` origin. | `false` (`true` in Development) |
| `AllowedOrigins` | Additional origins, exact match. | `[]` |

The schema's limits are not configuration. They are invariants, and as settings an operator could
relax a denial-of-service guard without a code review.

## Endpoints

| Endpoint | Purpose |
|---|---|
| `POST /graphql` | The API |
| `GET /graphql` | Nitro IDE — Development only |
| `GET /health/live` | Liveness: is the host answering |
| `GET /health/ready` | Readiness: database reachable and schema built |
| `GET /metrics` | Prometheus exposition |

The probes go through `HealthCheckService` rather than `MapHealthChecks` so they report *which*
check failed instead of one word. They stay plain HTTP because orchestrator probes cannot speak
GraphQL; `Query.health` is the same information for clients.

## Observability

Serilog to the console. Metrics via OpenTelemetry at `/metrics`, scraped by Prometheus, with a
dashboard auto-provisioned from
[`grafana/dashboards/graphql-gateway-overview.json`](../grafana/dashboards/graphql-gateway-overview.json).

There is exactly one custom instrument, `graphql_gateway.graphql.errors`, tagged by error code.
Everything else — request rate, latency, in-flight, GC, thread pool — comes from standard ASP.NET
Core and runtime instrumentation. The one thing that instrumentation cannot see is whether an
operation succeeded: a GraphQL response is HTTP 200 whether it carries data or an `errors` array,
so the error rate has to be counted from the errors themselves. Resolver failures and validation
rejections are counted; cost-limit rejections are not, because HotChocolate writes those straight
into the result without raising a diagnostic event or passing the error filter.

## Running locally

`docker compose up -d` from the repository root brings up the whole stack; the gateway is on
[http://localhost:8086/graphql](http://localhost:8086/graphql).

Against a database that is already running:

```bash
dotnet run --project src/Api
```

There is no `Migrations` section here, and `dotnet-ef` is deliberately absent from
`.config/dotnet-tools.json` — there is nothing in this service for it to do.

### Regenerating the schema

```bash
dotnet run --project src/Api -- schema export --output schema.graphql
```

Commit the result. CI runs the same command and fails on a diff.

## Testing

xUnit v3 with Moq and plain `Assert.*`, named `Method_Scenario_ExpectedResult`.

`tests/UnitTests` covers the pure logic: the aggregation window's range and period caps, the stored
sensor-type vocabulary, the metric units, and the error path — that a failure carrying an exception
is replaced by a generic message and a correlation id, with nothing leaking into any part of the
response.

`tests/IntegrationTests` runs against PostgreSQL via Testcontainers, tagged
`[Trait("Category", "Integration")]`, and needs a working Docker daemon. The schema is created from
this service's own model, which is what proves the duplicated mapping still describes a usable
database; `SchemaContractTests` then pins the physical column names, the `timestamptz` type and the
discriminator spellings, so a rename in DataProcessorService fails here rather than at runtime.
`KeysetPaginationTests` pages across an insert at the head of the feed — the case offset paging
gets wrong. `GraphQLExecutionTests` drives the real request executor and asserts on the JSON a
client receives, covering the resolvers, the paging middleware, the cost ceiling in both directions
and introspection being off outside Development.

Skip the containerised suites with `dotnet test --filter-not-trait "Category=Integration"`.

### Verifying a change

```bash
dotnet clean && dotnet build --nologo && dotnet format --verify-no-changes && dotnet test
```

The build must end in `0 Warning(s)`. Note that `dotnet csharpier check` does **not** pass on this
repository — it sorts `System.*` usings first while the `.editorconfig` and every existing file put
them last. `dotnet format` is the check CI enforces; run csharpier for layout if you like, then move
any `System.*` usings back to the end of the block.

## CI/CD

[`../.github/workflows/graphql-gateway.yaml`](../.github/workflows/graphql-gateway.yaml) restores,
verifies formatting, builds under SonarQube analysis, checks the exported schema against the
committed one, and on `main` pushes `feelitbreak/middle_project:graphql-gateway-service` to Docker
Hub, which watchtower then rolls out.
