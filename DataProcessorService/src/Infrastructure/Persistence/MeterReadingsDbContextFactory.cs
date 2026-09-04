namespace DataProcessorService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Builds a context for the <c>dotnet ef</c> tooling only.
/// <para>
/// Without this, adding a migration would boot the whole Api host, which fails fast on a missing
/// connection string by design. Scaffolding a migration needs the model, not a reachable database,
/// so the connection string here is a placeholder unless
/// <c>DATAPROCESSOR_DESIGNTIME_CONNECTION</c> is set --- which is only needed for commands that do
/// talk to a database, such as <c>dotnet ef database update</c>.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MeterReadingsDbContextFactory
    : IDesignTimeDbContextFactory<MeterReadingsDbContext>
{
    private const string ConnectionVariable = "DATAPROCESSOR_DESIGNTIME_CONNECTION";

    private const string PlaceholderConnection =
        "Host=localhost;Port=5432;Database=meterdb;Username=postgres;Password=postgres";

    /// <inheritdoc/>
    public MeterReadingsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionVariable) ?? PlaceholderConnection;

        var options = new DbContextOptionsBuilder<MeterReadingsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MeterReadingsDbContext(options);
    }
}
