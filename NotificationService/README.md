# NotificationService

Real-time signals for the dashboard. It consumes the `meter-readings-persisted` topic that
[DataProcessorService](../DataProcessorService) publishes after each committed batch, and fans each
event out to connected browsers over SignalR. See [../MiddleProject.md](../MiddleProject.md) for the
brief.

## Responsibilities

- Consume `meter-readings-persisted` under its own consumer group (`Messaging/`).
- Broadcast every event to connected clients on `/hubs/readings` (`Hubs/ReadingsHub.cs`).
- Report whether the consumer loop is actually running (`HealthChecks/`).
- Expose health probes and Prometheus metrics.

It owns no database, references no EF Core, and writes to no topic.

## How it fits in the pipeline

```
DataProcessorService --(Kafka: meter-readings-persisted)--> NotificationService --(SignalR)--> browser
                                                                                                 |
                                                                        (refetches) GraphQLGatewayService
```

## It pushes a signal, not data

The event says *what changed*; the client then refetches from the GraphQL gateway. That keeps the
gateway the single source of readings, so the React app never reconciles rows arriving over two
routes into one Apollo cache — no manual cache writes, no keeping pushed rows consistent with the
gateway's `(collectedAt, id)` ordering, and no gap to backfill after a dropped socket.

It consumes `meter-readings-persisted` rather than `meter-readings` for the same reason it exists at
all: the processor holds a batch for up to two seconds before writing it. A notifier on the raw
readings topic would fire while those rows are still in memory, the client would refetch, and the
gateway would answer without them — with no second event to correct it. The persisted event cannot
exist before the rows are queryable.

## The client-facing contract

This is what React binds to, so treat it as an API:

```ts
connection.on("readingsChanged", (e: {
  publishedAt: string;
  readingCount: number;
  newestCollectedAt: string;
  sensors: { location: string; sensorType: string }[];
}) => { /* refetch the gateway */ });
```

The payload is passed through as received rather than reshaped, so
[`Contracts/ReadingsPersistedMessage.cs`](src/NotificationService/Contracts/ReadingsPersistedMessage.cs)
is at once the topic's wire format and the browser's. It is a duplicate of the processor's contract,
carried here rather than shared, and must not drift from it.

**Every event goes to every client.** The payload names the affected locations, so a client filtered
to one room ignores what does not concern it. SignalR groups keyed by location or sensor type are
the obvious scale-up; with six locations and one dashboard they would be complexity without a
reason.

## Architecture

One project, not the layered split DataProcessorService and GraphQLGatewayService use. There is no
persistence, no domain model and one use case, so layers here would be indirection with a single
caller.

The consumer follows the processor's: cooperative-sticky assignment, manual offset commits, a
heartbeat behind the liveness probe. Two things differ, both on purpose:

- **One message at a time, no batching.** The processor batches because it writes to PostgreSQL, and
  it already coalesces a whole poll into one event. Batching again here would add its linger window
  to the latency this service exists to minimise.
- **`AutoOffsetReset.Latest`, not `Earliest`.** The setting only applies with no committed offset —
  first boot, or after the group's offsets expire. Replaying the backlog then would broadcast
  hundreds of "something changed" signals describing changes one refetch already covers. Nothing is
  lost, because the event is not data.

An undecodable message is logged and dropped rather than dead-lettered: the next batch publishes
another signal within seconds, so there is nothing worth replaying.

### Scaling out needs a backplane

One instance is fine for this project. A second one would consume its own share of the partitions
and broadcast to its own connections, so a client attached to instance A would silently never hear
about events instance B consumed — it fails quietly, not loudly. Running more than one requires a
Redis backplane.

## Configuration

### `Kafka`

| Key | Description | Default |
|---|---|---|
| `BootstrapServers` | Comma-separated broker list. Required. | — |
| `ReadingsPersistedTopic` | Topic consumed. | `meter-readings-persisted` |
| `ConsumerGroupId` | Consumer group. Never `data-processor`. | `notification-service` |
| `MaxPollIntervalMs` | Exceeding it has the broker rebalance our partitions away. | `300000` |
| `SessionTimeoutMs` | Consumer session timeout. | `45000` |

### `Cors`

| Key | Description | Default |
|---|---|---|
| `AllowLocalhost` | Allow any `localhost` origin. | `false` (`true` in Development) |
| `AllowedOrigins` | Additional origins, exact match. | `[]` |

A SignalR handshake needs `AllowCredentials()`, which a browser refuses alongside a wildcard origin.
That is why the policy enumerates origins through `SetIsOriginAllowed` instead of allowing any: get
this wrong and the WebSocket handshake fails with a CORS error that never mentions credentials.

## Endpoints

| Endpoint | Purpose |
|---|---|
| `/hubs/readings` | The SignalR hub |
| `GET /health/live` | Liveness: the consumer loop has completed an iteration recently |
| `GET /health/ready` | Readiness: the consumer owns at least one partition |
| `GET /metrics` | Prometheus exposition |
| `GET /` | Hub monitor — Development only |
| `GET /swagger` | Swagger UI for the probes — Development only |

Liveness is not "the host is up". A consumer loop that wedges, or one the broker evicted from its
group, leaves the web host answering requests and accepting WebSocket handshakes while no client
ever hears about new data. The probe reads the consumer's heartbeat instead.

### Documentation, and why the hub has none

Swagger documents the two probes and nothing else. A hub is a long-lived bidirectional connection
with no per-call schema, so SignalR's endpoints carry no API explorer metadata: `/hubs/readings`
answers a negotiate request while being invisible to OpenAPI, and no configuration changes that.

In its place, [`wwwroot/index.html`](src/NotificationService/wwwroot/index.html) is served at `/` in
Development — it connects to the hub and lists each `readingsChanged` as it lands, which makes
"is the notifier working" a browser tab rather than a bespoke client. Development-only, like the
gateway's Nitro IDE. The contract itself is the TypeScript snippet above.

## Observability

Serilog to the console. Metrics via OpenTelemetry at `/metrics`, scraped by Prometheus, with a
dashboard auto-provisioned from
[`grafana/dashboards/notification-service-overview.json`](../grafana/dashboards/notification-service-overview.json).

Three custom instruments: `notification.kafka.events_consumed`,
`notification.signalr.events_pushed` (the gap between them is messages that could not be decoded)
and `notification.kafka.consumer_lag`, the seconds between the processor committing a batch and this
service pushing the signal for it. Connected clients are **not** among them — ASP.NET Core's own
`Microsoft.AspNetCore.Http.Connections` meter already exports `signalr_server_active_connections`
and a connection-duration histogram, both tagged by transport.

## Running locally

`docker compose up -d` from the repository root brings up the whole stack; the hub is at
`http://localhost:8088/hubs/readings`.

Against a Kafka that is already running:

```bash
dotnet run --project src/NotificationService
```

Then open <http://localhost:8088> (or `http://localhost:5005` when running from the CLI) and watch
the events arrive. Confirm a `readingsChanged` lands per committed batch — one or two per injector
poll, never one per reading — and that refetching the gateway immediately afterwards returns the
rows the event referred to.

## Testing

xUnit v3 with plain `Assert.*`, named `Method_Scenario_ExpectedResult`.

`tests/UnitTests` covers what can be decided without a broker: the decoder's failure paths — an
empty body, malformed JSON, a bare `null` — the wire contract's camelCase property names, and the
probes, including that an unassigned consumer is Degraded rather than Unhealthy.

`tests/IntegrationTests` runs the whole service against a Testcontainers broker, tagged
`[Trait("Category", "Integration")]`, and needs a working Docker daemon. `WebApplicationFactory`
hosts the real composition root and a `Microsoft.AspNetCore.SignalR.Client` connection subscribes
over the in-memory server, so a message produced to the topic is asserted on as the browser would
see it — property names included. It also pins the two decisions that only exist at runtime: a
message published before start-up is never replayed to a client, and an undecodable message does not
wedge the partition behind it.

Run only the fast suite with `dotnet test tests/UnitTests`. A solution-wide
`--filter-not-trait "Category=Integration"` passes its tests but exits **8**, because the
integration assembly then discovers nothing — the same happens in the sibling services.

### Verifying a change

```bash
dotnet clean && dotnet build --nologo && dotnet format --verify-no-changes && dotnet test
```

The build must end in `0 Warning(s)`. Pass `--nologo` to `build` only, never to `dotnet test`: the
Microsoft.Testing.Platform runner in `global.json` receives the flag, rejects it, and reports "Zero
tests ran" with exit 5 — which looks like a broken test project and is not.

`dotnet csharpier check` does **not** pass on this repository —
it sorts `System.*` usings first while the `.editorconfig` and every existing file put them last.
`dotnet format` is the check CI enforces.

## CI/CD

[`../.github/workflows/notification-service.yaml`](../.github/workflows/notification-service.yaml)
restores, verifies formatting, builds under SonarQube analysis, and on `main` pushes
`feelitbreak/middle_project:notification-service` to Docker Hub, which watchtower then rolls out. It
needs the `SONAR_TOKEN_NOTIFICATION` secret and a SonarCloud project keyed
`feelitbreak_MiddleProject_NotificationService`.
