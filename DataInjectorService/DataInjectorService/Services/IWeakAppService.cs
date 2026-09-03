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
    /// <param name="cancellationToken">Propagates cancellation from the caller.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> carrying the resolved readings, a failed result
    /// with <see cref="ErrorCode.RateLimited"/> when the API enforces rate limiting (check
    /// <see cref="Error.RetryAfter"/> for the back-off duration), or a failed result with
    /// <see cref="ErrorCode.Failed"/> for all other error conditions (details are logged).
    /// </returns>
    Task<Result<IReadOnlyList<MeterReading>>> GetMetersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the WeakApp <c>/health</c> endpoint is reachable.
    /// </summary>
    /// <param name="cancellationToken">Propagates cancellation from the caller.</param>
    /// <returns><see langword="true"/> if the health endpoint returns a success status.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
