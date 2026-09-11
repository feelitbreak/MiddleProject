# MiddleProject

Microservices around an intentionally unstable external API. See [MiddleProject.md](MiddleProject.md)
for the brief and each service's own README for detail.

```
WeakApp --(HTTP)--> DataInjectorService --(Kafka)--> DataProcessorService --> PostgreSQL
```

Services are independently deployable: no shared assemblies between them. Duplicating a message
contract is preferred over coupling two services' builds.

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
- XML docs on public members. Comments explain *why*, not *what*.
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
Kafka UI on 8070, Prometheus 9090, Grafana 3000, injector 8082, processor 8084.
