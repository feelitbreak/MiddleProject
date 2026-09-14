# DataInjectorService

The Data Injector microservice of the [MiddleProject](../MiddleProject.md) system. It polls the
unstable [WeakApp](https://github.com/nantonov/WeakApp) API for meter readings and publishes each
reading onto a Kafka topic for [DataProcessorService](../DataProcessorService) to consume and
persist.

## Responsibilities

- Poll WeakApp's `GET /meters` endpoint on a fixed interval (`MeterPollingService`, an
  `IHostedService` background worker).
- Handle every documented WeakApp response variant explicitly (`WeakAppService`):
  - `200 OK` — deserialize and publish the readings.
  - `429 Too Many Requests` — back off for the duration given by `Retry-After` (or a configured
    fallback) instead of retrying immediately.
  - `5xx` / unexpected status / corrupted body (`{"error":"data corrupted"}`) / malformed JSON —
    logged and skipped; polling resumes on the next interval.
- Resolve the polymorphic `payload` field of each reading into a strongly-typed model based on its
  `type` discriminator (`air_quality`, `motion`, `energy`), via `MeterReadingTypeResolver`. Unknown
  types fall back to `UnknownPayload` rather than failing the whole batch.
- Publish each resolved reading to Kafka as a JSON message, keyed by `"{type}:{name}"` so that all
  readings for a given sensor land on the same partition and stay ordered.
- Expose liveness/readiness health checks and Prometheus metrics for observability.

## How it fits in the pipeline

```
WeakApp API --(HTTP poll)--> DataInjectorService --(Kafka: meter-readings)--> DataProcessorService --> Database
```

DataInjectorService only reads from WeakApp and writes to Kafka; it holds no state of its own.

## Configuration

Settings are bound from `appsettings.json` / environment variables (`__` separator), validated via
`ValidateDataAnnotations().ValidateOnStart()` so misconfiguration fails fast at startup.

### `WeakApp`

| Key | Description | Default |
|---|---|---|
| `BaseUrl` | Base URL of the WeakApp instance | *(required)* |
| `ApiKey` | Sent as the `X-Api-Key` request header | *(required)* |
| `PollingIntervalSeconds` | Delay between successive `/meters` polls | `60` |
| `TimeoutSeconds` | Per-attempt HTTP timeout | `10` |
| `RetryCount` | Retry attempts for transient failures | `3` |
| `RetryBaseDelaySeconds` | Base delay before the first retry (exponential back-off + jitter) | `2` |
| `RateLimitDelaySeconds` | Fallback back-off when a `429` has no `Retry-After` header | `60` |

Retries, circuit breaking and per-attempt timeouts are wired via
`Microsoft.Extensions.Http.Resilience`'s standard resilience handler
(`Extensions.AddDataInjectorServices`); `429` responses are handled explicitly in
`WeakAppService` and are excluded from that retry pipeline.

### `Kafka`

| Key | Description | Default |
|---|---|---|
| `BootstrapServers` | Comma-separated Kafka broker addresses | `localhost:9092` |
| `MeterReadingsTopic` | Topic readings are published to | `meter-readings` |
| `Acks` | `All` \| `Leader` \| `None` (bound case-insensitively) | `All` |
| `CompressionType` | `None` \| `Gzip` \| `Snappy` \| `Lz4` \| `Zstd` | `Zstd` |
| `MaxInFlightRequestsPerConnection` | Max in-flight produce requests per connection | `5` |
| `EnableIdempotence` | Exactly-once producer semantics / ordering guarantee | `true` |
| `MessageSendMaxRetries` | Retries for a failed produce request | `3` |
| `RetryBackoffMs` | Backoff between produce retries | `500` |
| `MessageTimeoutMs` | Total time a message may spend being produced, including retries | `30000` |

`EnableIdempotence` requires `Acks=All` and at most 5 in-flight requests per connection; both
combinations are rejected at startup by `KafkaOptions.Validate` rather than surfacing as an opaque
librdkafka error. Every message carries `content-type: application/json` and `schema-version`
headers so consumers can reject payload shapes they do not understand.

### `Cors`

| Key | Description | Default |
|---|---|---|
| `AllowLocalhost` | Allow any `localhost` origin (dev convenience) | `false` |
| `AllowedOrigins` | Explicit allow-list of additional origins | `[]` |

In `docker-compose.yml` (repo root) this service runs as `data_injector`, pointed at
`WeakApp__BaseUrl=http://weak_app:8080` and `Kafka__BootstrapServers=kafka:29092`.

## Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Liveness — always healthy once the process is up |
| `GET /health/ready` | Readiness — degraded if WeakApp is unreachable (`WeakAppHealthCheck`) |
| `GET /metrics` | Prometheus scraping endpoint |
| `GET /swagger` | Swagger UI (Development environment only) |

No inbound business API is exposed — this service is a one-way pipe from WeakApp to Kafka; it does
not expose endpoints beyond health, metrics and Swagger.

The probes are mapped as ordinary handlers over `HealthCheckService` rather than with
`MapHealthChecks`: that extension writes a bare status string and registers a raw request delegate
with no method to describe, so its endpoints never reach the API explorer and never appear in
Swagger. Going through the service directly documents them and returns which check failed.

## Observability

- **Logging**: Serilog, console sink, structured with source context.
- **Metrics** (`DataInjectorMetrics`, OpenTelemetry meter `DataInjectorService`, scraped by
  Prometheus):
  - `data_injector.weakapp.requests` — poll requests, tagged by `outcome` (`success`/`failure`).
  - `data_injector.weakapp.readings_polled` — readings fetched per successful poll.
  - `data_injector.kafka.messages_produced` / `data_injector.kafka.messages_failed`.
  - `data_injector.polling.cycle_duration` — full poll+publish cycle duration.
  - `data_injector.kafka.produce_duration` — per-message produce latency.
  - Plus ASP.NET Core, `HttpClient` and .NET runtime instrumentation.

## Running locally

Via the full stack from the repository root:

```bash
docker compose up data_injector weak_app kafka
```

Or standalone against an already-running WeakApp + Kafka:

```bash
cd DataInjectorService/DataInjectorService
dotnet run
```

Set `WeakApp:BaseUrl`, `WeakApp:ApiKey` and `Kafka:BootstrapServers` via `appsettings.Development.json`,
user secrets, or environment variables before running.

## Testing

```bash
cd DataInjectorService
dotnet test
```

Covers response-variant handling in `WeakAppService`, polling/back-off behavior in
`MeterPollingService`, payload type resolution, CORS configuration, the health check, and metrics
recording. `KafkaProducerIntegrationTests` additionally exercises the producer against a real
broker.

## CI/CD

[`.github/workflows/data-injector.yaml`](../.github/workflows/data-injector.yaml) triggers on
changes under `DataInjectorService/**`:

1. Restore, `dotnet format --verify-no-changes`, build, run tests with coverage, and analyze with
   SonarQube.
2. On push to `main`: build and push the Docker image to Docker Hub
   (`data-injector-service` tag), which `watchtower` then pulls in deployed environments.
