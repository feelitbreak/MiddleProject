namespace DataInjectorService.Services;

using DataInjectorService.Common;
using DataInjectorService.Models;

/// <summary>
/// Abstracts communication with the WeakApp external API.
/// </summary>
public interface IWeakAppService
{
    /// <summary>
    /// Fetches the current meter readings from <c>GET /meters</c>.
    /// </summary>
    Task<Result<IReadOnlyList<MeterReading>>> GetMetersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the WeakApp <c>/health</c> endpoint is reachable.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
