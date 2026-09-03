namespace DataInjectorService.Services;

using DataInjectorService.Common;
using DataInjectorService.Configuration;
using DataInjectorService.Helpers;
using DataInjectorService.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

/// <summary>
/// Calls the WeakApp external API and maps every documented response variant to a
/// <see cref="Result{T}"/>. All failure details are logged here; callers only need to
/// distinguish between success, rate-limiting, and general failure.
/// <para>Uses <see cref="IHttpClientFactory"/> so that socket lifetimes are managed by the framework.</para>
/// </summary>
/// <param name="httpClientFactory">Factory used to create named HTTP clients.</param>
/// <param name="options">WeakApp configuration options.</param>
/// <param name="logger">Logger instance.</param>
public sealed class WeakAppService(
    IHttpClientFactory httpClientFactory,
    IOptions<WeakAppOptions> options,
    ILogger<WeakAppService> logger
) : IWeakAppService
{
    private const string MetersPath = "/meters";
    private const string HealthPath = "/health";
    private const string ClientName = "WeakApp";
    private const string ErrorKey = "error";
    private const string CorruptedValue = "data corrupted";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WeakAppOptions options = options.Value;

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<MeterReading>>> GetMetersAsync(
        CancellationToken cancellationToken
    )
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            using var response = await client.GetAsync(
                MetersPath,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            return response.StatusCode switch
            {
                HttpStatusCode.OK => await this.HandleOkAsync(response, cancellationToken),
                HttpStatusCode.TooManyRequests => this.HandleRateLimited(response),
                _ when (int)response.StatusCode >= 500 => this.HandleServerError(response),
                _ => this.HandleUnexpected(response),
            };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.RequestTimedOut(ex, MetersPath);
            return Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.UnexpectedException(ex, MetersPath);
            return Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            using var response = await client.GetAsync(HealthPath, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.HealthCheckFailed(ex);
            return false;
        }
    }

    private static bool IsCorruptedResponse(string body)
    {
        var trimmed = body.Trim();
        if (!trimmed.StartsWith('{'))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return doc.RootElement.TryGetProperty(ErrorKey, out var errorProp)
                && errorProp.GetString() == CorruptedValue;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<Result<IReadOnlyList<MeterReading>>> HandleOkAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (IsCorruptedResponse(rawBody))
        {
            logger.CorruptedData(rawBody);
            return Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed);
        }

        List<MeterReading>? rawReadings;
        try
        {
            rawReadings = JsonSerializer.Deserialize<List<MeterReading>>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.DeserializationFailed(ex, rawBody);
            return Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed);
        }

        if (rawReadings is null || rawReadings.Count == 0)
        {
            logger.EmptyMeterList();
            return Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed);
        }

        var resolved = MeterReadingTypeResolver.Resolve(rawReadings);
        logger.ReadingsFetched(resolved.Count);
        return Result.Success(resolved);
    }

    private Result<IReadOnlyList<MeterReading>> HandleRateLimited(HttpResponseMessage response)
    {
        var retryAfter =
            response.Headers.RetryAfter?.Delta
            ?? TimeSpan.FromSeconds(this.options.RateLimitDelaySeconds);

        logger.RateLimited(retryAfter.TotalSeconds);
        return Result.Failure<IReadOnlyList<MeterReading>>(Error.CreateRateLimited(retryAfter));
    }

    private Result<IReadOnlyList<MeterReading>> HandleServerError(HttpResponseMessage response)
    {
        logger.ServerError((int)response.StatusCode, response.ReasonPhrase);
        return Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed);
    }

    private Result<IReadOnlyList<MeterReading>> HandleUnexpected(HttpResponseMessage response)
    {
        logger.UnexpectedStatus((int)response.StatusCode);
        return Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed);
    }
}
