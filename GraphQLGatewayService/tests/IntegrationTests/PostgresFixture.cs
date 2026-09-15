namespace GraphQLGatewayService.IntegrationTests;

using GraphQLGatewayService.Domain.Enums;
using GraphQLGatewayService.Infrastructure.Persistence;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

/// <summary>
/// A throwaway PostgreSQL instance, schema-created from the EF model.
/// <para>
/// Creating the schema from this service's own model is deliberate: the gateway owns no
/// migrations, so this is what proves the duplicated mapping still describes a usable database.
/// The session time zone is pinned to UTC, matching production, because <c>date_trunc</c>
/// truncates in the session time zone.
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

    /// <summary>Gets the connection string for the running container, with the session in UTC.</summary>
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
    /// Creates a context whose session runs in <paramref name="timeZone"/>. Only the aggregation
    /// tests need this: pointing a session at a non-UTC zone is the only way to prove the explicit
    /// UTC argument to <c>date_trunc</c> is doing its job.
    /// </summary>
    public MeterReadingsDbContext CreateContext(string timeZone) =>
        new(
            new DbContextOptionsBuilder<MeterReadingsDbContext>()
                .UseNpgsql($"{this.container.GetConnectionString()};Options=-c timezone={timeZone}")
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .Options
        );

    /// <summary>Removes every row, keeping the schema, so each test starts from a known state.</summary>
    public async Task ResetAsync()
    {
        await using var context = this.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE meter_readings, sensors RESTART IDENTITY CASCADE;"
        );
    }

    /// <summary>Inserts the given sensors and readings, returning the sensors by name and type.</summary>
    public async Task SeedAsync(params IEnumerable<MeterReadingRow> readings)
    {
        await using var context = this.CreateContext();
        context.AddRange(readings);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Creates a sensor row without saving it.</summary>
    public static SensorRow Sensor(string name, SensorType type) =>
        new() { Name = name, Type = type };
}
