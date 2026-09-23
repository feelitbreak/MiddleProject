# MeterReadingsUI

The dashboard the rest of the system exists to feed. React and TypeScript on Vite, reading the
[GraphQLGatewayService](../GraphQLGatewayService) schema and listening to
[NotificationService](../NotificationService) for the signal that something changed. See
[the repository README](../README.md) for how the whole system fits together.

## Responsibilities

- Show what every location is reading now, and how it has moved (`components/console/`).
- Filter, page and search the reading history (`components/explorer/`).
- Refetch when the hub says readings landed (`hooks/useLiveReadings.ts`).
- Stay honest while the upstream API misbehaves: fresh, stale and lost are designed states.

It owns no data and writes nothing. Every number on screen came from the gateway.

## How it fits in the pipeline

```
WeakApp --> DataInjectorService --(Kafka)--> DataProcessorService --> PostgreSQL
                                                     |                    |
                                     (Kafka: meter-readings-persisted)  (read-only)
                                                     v                    v
                                          NotificationService     GraphQLGatewayService
                                                     |                    |
                                              (SignalR) --> MeterReadingsUI <-- (GraphQL)
```

## Routes

| Route       | Purpose                                                                                  |
| ----------- | ---------------------------------------------------------------------------------------- |
| `/`         | Console: latest values per location, aggregates by location and by sensor type, arrivals |
| `/readings` | Explorer: the reading history, filtered and paged                                        |

Both routes share one filter object. The schema is shaped for exactly that — `readings` and
`readingAggregates` both take a `where` — so `hooks/useFilter.ts` holds a single piece of state and
projects it into the two input types rather than keeping per-panel copies.

## Architecture

| Folder           | Holds                                                                                            |
| ---------------- | ------------------------------------------------------------------------------------------------ |
| `src/domain`     | The vocabulary: alert thresholds, feed freshness, the location pivot, metric metadata. No React. |
| `src/graphql`    | The documents, and the types generated from the gateway's committed schema.                      |
| `src/hooks`      | The filter, the clock, and the single SignalR seam.                                              |
| `src/components` | `shared/` chrome and controls, then `console/` and `explorer/`.                                  |
| `src/styles`     | One stylesheet: design tokens and the pixel chrome primitives.                                   |

There is no state library. One filter object and Apollo's cache cover everything this app does; a
store between them would be indirection with one caller.

### Live updates

`NotificationService` pushes a signal, never rows. `useLiveReadings` is the only place that knows
SignalR exists: it connects to `/hubs/readings`, debounces `readingsChanged` by 400 ms, and asks
Apollo to refetch the active queries. Nothing writes pushed data into the cache, so there is no
reconciliation against the gateway's `(collectedAt, id)` ordering and no gap to backfill.

Three properties of the hub the client has to respect, all of them deliberate upstream:

- **At-least-once delivery over three partitions.** Duplicates arrive and `publishedAt` is not
  monotonic, so the debounce coalesces a burst into one query.
- **No replay after a disconnect.** The consumer reads from `Latest`, so events during a gap are
  gone; `onreconnected` fires one refetch, which covers whatever was missed.
- **No groups.** Every client receives every event, so filtering is the client's job.

### Feed age and value band are separate

`LAST READ` reports how old a reading is; `READING` reports whether it sits inside a threshold.
They are unrelated, and a sensor can be both stale and out of band — one column could only report
the worse of the two.

Feed age is derived from `collectedAt` against the injector's 60-second poll: **live** under two
minutes, **stale** to fifteen, **lost** beyond. A lost sensor shows no value rather than one that
stopped being true.

### The thresholds are ours

**The gateway exposes raw numbers and no notion of a healthy value.** Every threshold lives in
`src/domain/thresholds.ts`, and every surface that judges a value prints the number it used, so a
red figure never implies the gateway said so.

| Metric         | Threshold       | Reference                                               |
| -------------- | --------------- | ------------------------------------------------------- |
| CO2            | > 1000 ppm      | Common indoor ventilation indicator, not a safety limit |
| PM2.5          | > 15 ug/m3      | WHO 24-hour air quality guideline                       |
| Humidity       | outside 30-60 % | Usual indoor comfort range                              |
| Energy, motion | none            | There is no such thing as an unhealthy kWh              |

These are real-world reference points, not values tuned to look good against the demonstration
data. WeakApp emits uniformly random values — CO2 between 300 and 900 — so **CO2 never trips**,
PM2.5 trips often, and humidity trips about half the time. That is the honest result of applying
real guidance to synthetic noise, and the numbers are one edit away in that file.

### One row per location

Every location reports all three sensor types, so `latestReadings` returns three rows per location.
`domain/locations.ts` pivots them into one row with every column filled, keeping each sensor's own
age so a single dead feed cannot hide behind two live ones.

## Design

A pixel workstation: a tiled desktop of 8-bit windows, each title-barred with the query it runs.
Colour is never decorative. Chrome is desaturated and never touches a reading; state is saturated
and only ever lands on data.

| Colour | Means                                                       |
| ------ | ----------------------------------------------------------- |
| Mint   | In band, live                                               |
| Butter | Stale                                                       |
| Blush  | Out of band, lost, error                                    |
| Lilac  | Reserved: what the viewer has filtered to, and nothing else |

Type is [Handjet](https://fonts.google.com/specimen/Handjet) throughout, self-hosted as a 7.6 kB
latin subset in [`public/fonts`](public/fonts) under the SIL Open Font License. Loading it from
Google Fonts cost a render-blocking stylesheet plus a chained request to a second origin, which was
the largest single contributor to first contentful paint. Every size runs larger than a normal UI
face because Handjet is narrow; nothing sits below 12px.

Class names describe what they mark rather than abbreviating it: `.filter-label`, `.window-titlebar`,
`.feed-chip-stale`. The stylesheet is the only place layout lives, and every grid track is a
fraction or a fixed size -- an `auto` track spanned by a scrolling panel resolves to that panel's
full content height, which is what once made this page taller than the viewport after a refetch.

Charts are hand-drawn SVG rather than a library: the design needs mitred steps, square markers and
a hard outline, which is most of a charting library's defaults overridden anyway.

## Configuration

`.env` holds the development configuration; copy [`.env.example`](.env.example) to start.

| Variable               | Read by               | Default                 |
| ---------------------- | --------------------- | ----------------------- |
| `VITE_GRAPHQL_URL`     | the browser           | `/graphql`              |
| `VITE_HUB_URL`         | the browser           | `/hubs/readings`        |
| `GRAPHQL_PROXY_TARGET` | `vite.config.ts` only | `http://localhost:8086` |
| `HUB_PROXY_TARGET`     | `vite.config.ts` only | `http://localhost:8088` |

The two `VITE_` values default to same-origin paths, which the dev proxy and nginx both forward, so
there is no CORS and no back-end host in the bundle. The two targets are where the dev server
forwards them; they carry no prefix, so they never reach client code.

**Vite inlines `VITE_` values at build time**, so the image cannot be repointed without rebuilding.
For the container, change [`nginx.conf`](nginx.conf) instead -- it proxies `/graphql` to
`graphql_gateway:8080` and `/hubs` to `notification_service:8080`.

## Running locally

`docker compose up -d` from the repository root brings up the whole stack; the UI is on
[http://localhost:8090](http://localhost:8090).

Against services that are already running:

```bash
npm install
npm run dev
```

That serves [http://localhost:5180](http://localhost:5180) and proxies to the gateway on 8086 and
the hub on 8088.

### Regenerating the types

```bash
npm run codegen
```

Reads `../GraphQLGatewayService/src/Api/schema.graphql`, so it works offline. Commit the result:
CI runs the same command and fails on a diff, which is what turns a gateway schema change into a
compile error here rather than a runtime surprise.

## Testing

Jest with ts-jest, jsdom and Testing Library. Tests sit beside what they cover as
`Name.test.ts(x)`; shared builders live in `src/testing`. Coverage is always collected because
SonarQube reads `coverage/lcov.info`; open `coverage/lcov-report/index.html` to browse it.

147 tests across 16 suites, around 93% of lines. What they pin:

- **Domain** -- threshold boundaries (a value exactly on the limit is inside it), the freshness
  boundaries at two and fifteen minutes, and the location pivot keeping each sensor's own age.
- **`useLiveReadings`** -- against a mocked hub: a burst of events costs one refetch, a reconnect
  costs one more, and a pending refetch is cancelled on unmount.
- **Components** -- a lost sensor renders no value while a stale one still renders its last, feed
  age and value band are reported independently, and errors surface the gateway's own
  `extensions.code`.
- **Pages** -- against `MockedProvider`: pagination state, the empty and error surfaces, and the
  identity columns collapsing once one location is filtered.

Jest runs in ESM, which has two consequences worth knowing: `jest` is not a global, so a test that
mocks imports `{ jest }` from `@jest/globals`, and module mocks use `jest.unstable_mockModule`
followed by a dynamic `import` of the module under test.

```bash
npm test
npm run test:watch
```

### Verifying a change

```bash
npm run format:check && npm run lint && npm run typecheck && npm test && npm run build
```

## Dependency advisories

`npm run audit` writes `audit-report.json`; `npm run audit:summary` turns it into a readable table.

Advisories are published on someone else's schedule, so a new one can appear against a commit
nobody has touched. CI therefore **reports and never blocks** -- otherwise a CVE disclosed on a
Tuesday would stop Wednesday's release, and stop a rollback to the tag that was fine on Monday.

Nothing enforces a severity threshold. On a live product that belongs in a scheduled job off the
release path, so a red run is a signal to plan a fix rather than something that stops a deploy.

## CI/CD

[`../.github/workflows/meter-readings-ui.yaml`](../.github/workflows/meter-readings-ui.yaml)
installs, checks formatting, lints, typechecks, tests, builds, verifies the committed types still
match the gateway schema, and runs a SonarQube scan over `coverage/lcov.info`. On `main` it pushes
`feelitbreak/middle_project:meter-readings-ui` to Docker Hub, which watchtower then rolls out. It
also runs when the gateway's schema changes, since that is the other thing that can break this
build.
