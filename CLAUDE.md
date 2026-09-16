# MiddleProject

Microservices around an intentionally unstable external API. See [MiddleProject.md](MiddleProject.md)
for the brief and each service's own README for detail.

```
WeakApp --(HTTP)--> DataInjectorService --(Kafka)--> DataProcessorService --> PostgreSQL
                                                             |
                                                          (Kafka)
                                                             v
                                                    NotificationService --(SignalR)--> browser
```

Services are independently deployable: no shared assemblies between them. Duplicating a message
contract is preferred over coupling two services' builds.

## Working agreement

- **Nothing is committed without approval.** Leave changes in the working tree and wait; the user
  reviews and decides when to commit or push.
- **Write no tests until the change itself is approved.** Get the implementation reviewed first,
  then add the tests for it. Keeping existing tests compiling and green is part of the change, not
  new test coverage.

## Comments and documentation

Applies to every project here — .NET services, the React app when it lands, scripts, READMEs.

- **Most code carries no comment at all.** Names do the explaining. A comment earns its place
  only when a name cannot carry the point: a unit, an invariant, a non-obvious constraint, or
  why the obvious approach was rejected.
- **One or two sentences, never a paragraph.** A comment that needs structure — a `<para>`, a
  second paragraph, a list — is too long. Cut it down rather than organising it.
- **Never restate the signature.** Nothing on a self-evident parameter or property, no
  `/// Gets the location` on `Location`.
- **Prose sits near a fifth of the lines.** A file where it approaches half is a defect.

## General

- **Prefer built-in framework APIs over new helper classes.** Before adding a helper, converter or
  extension, check whether the BCL, EF Core, ASP.NET Core or `System.Text.Json` already does it.
  Reach for a helper only when nothing built-in fits, and say why in a comment.
- Don't add speculative abstractions. One implementation means no interface yet.
- Verify claims about framework behaviour by running the code, not from memory.

## .NET services

- .NET 10. `TreatWarningsAsErrors` is on; builds must be warning-free.
- File-scoped namespaces, usings **inside** the namespace, `System.*` last.
- `sealed` classes, primary constructors for DI, `this.` on fields. No records.
- XML docs follow [Comments and documentation](#comments-and-documentation): a `<summary>` is
  one or two sentences, most properties need none, and a ten-line enum with one summary is fine.
- `Result`/`Error` for expected failures; exceptions for bugs.
- Serilog, OpenTelemetry metrics at `/metrics`, health checks at `/health/live` and `/health/ready`.
- EF Core code-first. Read-only queries use `AsNoTracking()`; any `Take`/`Skip` needs an `OrderBy`.
- Tests: xUnit v3, Moq, plain `Assert.*`, `Method_Scenario_ExpectedResult`. Integration tests use
  Testcontainers and are tagged `[Trait("Category", "Integration")]`.

### Verifying a change

Run all three, from the service directory. `dotnet build` alone is not enough — incremental builds
skip analyzers on unchanged projects, so a clean is what actually surfaces analyzer findings:

```bash
dotnet clean && dotnet build --nologo && dotnet format --verify-no-changes && dotnet test
```

The build must end in `0 Warning(s)`. Never report work as done without having seen that line:
`TreatWarningsAsErrors` means a warning is a broken build, and analyzer output is easy to miss when
skimming for the word "error".

`SonarAnalyzer.CSharp` is referenced by every project so the SonarQube
rules CI enforces also run locally. Some Sonar rules ship **disabled by default** in that package
while the server's quality profile has them on — S107 is one — so the ones we rely on are enabled
explicitly in `.editorconfig`. If SonarQube reports a rule the local build did not, enable it there
rather than fixing it blind.

## Frontend (React + TypeScript)

Not yet present. When added: strict TypeScript, no `any`, function components with hooks, and the
same preference for platform and library built-ins over bespoke utilities.

## Local stack

`docker compose up -d` from the repo root. Postgres `meterdb` (`postgres`/`postgres`, local only),
Kafka UI on 8070, Prometheus 9090, Grafana 3000, injector 8082, processor 8084,
gateway 8086, notifications 8088.
