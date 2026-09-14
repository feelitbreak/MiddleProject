namespace DataProcessorService.IntegrationTests;

using DataProcessorService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

/// <summary>
/// A throwaway PostgreSQL instance, schema-created from the EF model.
/// <para>
/// The session time zone is pinned to UTC, matching production. PostgreSQL's <c>date_trunc</c>
/// truncates in the session time zone, so a container defaulting to local time would produce
/// bucket boundaries that pass here and fail elsewhere.
/// </para>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string PostgresImage = "postgres:18-alpine";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("meterdb_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>Gets the connection string for the running container.</summary>
    public string ConnectionString =>
        $"{this.container.GetConnectionString()};Options=-c timezone=UTC";

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await this.container.StartAsync();

        await using var context = this.CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await this.container.DisposeAsync();
    }

    /// <summary>Creates a context bound to the container, with the session in UTC.</summary>
    public MeterReadingsDbContext CreateContext() => this.CreateContext("UTC");

    /// <summary>
    /// Creates a context whose session runs in <paramref name="timeZone"/>. Only aggregation tests
    /// need this: <c>date_trunc</c> truncates in the session time zone, so pointing a test at a
    /// non-UTC session is the only way to prove the explicit UTC argument is doing its job.
    /// </summary>
    public MeterReadingsDbContext CreateContext(string timeZone) =>
        new(
            new DbContextOptionsBuilder<MeterReadingsDbContext>()
                .UseNpgsql($"{this.container.GetConnectionString()};Options=-c timezone={timeZone}")
                .Options
        );

    /// <summary>
    /// Removes every row so that each test starts from a known state, while keeping the schema.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var context = this.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE meter_readings, sensors RESTART IDENTITY CASCADE;"
        );
    }
}
