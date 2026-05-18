using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shopee.Affiliate.Infrastructure;
using Shopee.Affiliate.Infrastructure.Reports;
using Shopee.Affiliate.Signing;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Shopee.Affiliate.Reports;

/// <summary>
/// Default <see cref="IShopeeAffiliateReportsClient"/> implementation. Signs
/// every request with the Shopee SHA256 scheme, posts a single GraphQL body to
/// the configured endpoint, and translates the response into the public DTOs.
/// </summary>
/// <remarks>
/// <para>HTTP transport rules:</para>
/// <list type="bullet">
///   <item>HTTP 2xx with GraphQL <c>errors[]</c> → mapped to a typed exception.</item>
///   <item>HTTP 5xx or timeout → one automatic retry, then a typed exception.</item>
///   <item>HTTP 4xx (other than 401/403/429) → immediate exception, no retry.</item>
/// </list>
/// <para>
/// The Shopee Affiliate Open API operates in <c>GMT+7</c> (Singapore). The SDK
/// receives <see cref="DateTimeOffset"/> values whose offsets are honored when
/// converting to Unix-seconds, so callers can pass any TZ and the absolute
/// instant is preserved on the wire.
/// </para>
/// </remarks>
public sealed class ShopeeAffiliateReportsClient : IShopeeAffiliateReportsClient
{
    /// <summary>Shopee operates in <c>UTC+7</c>; exposed for tests and docs.</summary>
    public static readonly TimeSpan ShopeeTimezoneOffset = TimeSpan.FromHours(7);

    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;
    private const int SummarySafetyPages = 200;

    private readonly HttpClient _httpClient;
    private readonly ShopeeAffiliateReportsOptions _options;
    private readonly Func<DateTimeOffset> _clock;
    private readonly ILogger<ShopeeAffiliateReportsClient> _logger;

    [ActivatorUtilitiesConstructor]
    public ShopeeAffiliateReportsClient(
        HttpClient httpClient,
        IOptions<ShopeeAffiliateReportsOptions> options,
        ILogger<ShopeeAffiliateReportsClient>? logger = null)
        : this(httpClient, (options ?? throw new ArgumentNullException(nameof(options))).Value, clock: null, logger)
    {
    }

    public ShopeeAffiliateReportsClient(
        HttpClient httpClient,
        ShopeeAffiliateReportsOptions options,
        Func<DateTimeOffset>? clock = null,
        ILogger<ShopeeAffiliateReportsClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _logger = logger ?? NullLogger<ShopeeAffiliateReportsClient>.Instance;
    }

    public async Task<ShopeeConversionPage> ListConversionsAsync(
        ListShopeeConversionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _options.Validate();
        EnsureWindow(request.From, request.To);

        var pageSize = NormalizePageSize(request.PageSize);
        var status = ShopeeReportsGraphQlPayloadFactory.MapStatusFilter(request.Status);
        var page = Math.Max(1, request.Page);

        var cursor = request.Cursor;
        if (cursor is null && page > 1)
        {
            cursor = await ScrollToPageAsync(request, status, pageSize, page, cancellationToken);
        }

        var mapped = await FetchPageAsync(request, status, pageSize, cursor, page, cancellationToken);
        var filtered = ShopeeReportsConversionFilter.ApplySubId(mapped.Items, request.SubId);
        return ReferenceEquals(filtered, mapped.Items) ? mapped : mapped with { Items = filtered };
    }

    public async Task<ShopeeConversionDetail> GetConversionAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }
        _options.Validate();

        var payload = ShopeeReportsGraphQlPayloadFactory.BuildGetConversionPayload(orderId);
        using var response = await SendAsync(payload, cancellationToken);
        var detail = ShopeeReportsResponseMapper.MapConversionDetail(response.Body.RootElement);

        if (detail is null)
        {
            throw new ShopeeAffiliateNotFoundException(
                $"Shopee conversionReport returned no rows for orderId '{orderId}'.",
                code: "NOT_FOUND");
        }

        return detail;
    }

    public async Task<ShopeeSalesSummary> GetSalesSummaryAsync(
        ShopeeSalesSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _options.Validate();
        EnsureWindow(request.From, request.To);

        var conversions = new List<ShopeeConversion>();
        string? cursor = null;

        for (var fetched = 0; fetched < SummarySafetyPages; fetched++)
        {
            var payload = ShopeeReportsGraphQlPayloadFactory.BuildListConversionsPayload(
                request.From.ToUnixTimeSeconds(),
                request.To.ToUnixTimeSeconds(),
                orderStatus: "ALL",
                subId: null,
                scrollId: cursor,
                limit: MaxPageSize);

            using var response = await SendAsync(payload, cancellationToken);
            var page = ShopeeReportsResponseMapper.MapConversionPage(response.Body.RootElement, requestedPage: 1, requestedLimit: MaxPageSize);
            conversions.AddRange(ShopeeReportsConversionFilter.ApplySubId(page.Items, request.SubId));

            if (!page.HasMore || string.IsNullOrEmpty(page.NextCursor)) break;
            cursor = page.NextCursor;
        }

        return ShopeeSalesSummaryAggregator.Aggregate(conversions, request.From, request.To);
    }

    public Task<ShopeeClickStats> GetClickStatsAsync(
        ShopeeClickStatsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new ShopeeClickStats(
            Granularity: request.Granularity,
            Points: Array.Empty<ShopeeClickPoint>(),
            Supported: false,
            UnsupportedReason: "Shopee Affiliate Open API does not expose a click report endpoint."));
    }

    public Task<ShopeeLinkUsage> GetGeneratedLinkUsageAsync(
        ShopeeLinkUsageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new ShopeeLinkUsage(
            LinksGenerated: 0,
            ClicksAttributed: 0,
            ConversionsAttributed: 0,
            CommissionAttributed: Money.Zero(),
            Supported: false,
            UnsupportedReason: "Shopee Affiliate Open API does not expose link-generation counters."));
    }

    private async Task<ShopeeConversionPage> FetchPageAsync(
        ListShopeeConversionsRequest request,
        string status,
        int pageSize,
        string? cursor,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var payload = ShopeeReportsGraphQlPayloadFactory.BuildListConversionsPayload(
            request.From.ToUnixTimeSeconds(),
            request.To.ToUnixTimeSeconds(),
            status,
            request.SubId,
            cursor,
            pageSize);

        using var response = await SendAsync(payload, cancellationToken);
        return ShopeeReportsResponseMapper.MapConversionPage(response.Body.RootElement, pageNumber, pageSize);
    }

    private async Task<string?> ScrollToPageAsync(
        ListShopeeConversionsRequest request,
        string status,
        int pageSize,
        int targetPage,
        CancellationToken cancellationToken)
    {
        string? cursor = null;
        for (var current = 1; current < targetPage; current++)
        {
            var page = await FetchPageAsync(request, status, pageSize, cursor, current, cancellationToken);
            if (!page.HasMore || string.IsNullOrEmpty(page.NextCursor))
            {
                return page.NextCursor;
            }
            cursor = page.NextCursor;
        }
        return cursor;
    }

    private async Task<ShopeeAffiliateGraphQlResponse> SendAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendOnceAsync(payload, cancellationToken);
        }
        catch (ShopeeAffiliateApiException ex) when (ex.Code is "HTTP_5XX" or "HTTP_TIMEOUT")
        {
            _logger.LogWarning(
                "Shopee reports request failed (code {Code}); retrying once. AppId={AppId}",
                ex.Code, MaskAppId(_options.AppId));
            return await SendOnceAsync(payload, cancellationToken);
        }
    }

    private async Task<ShopeeAffiliateGraphQlResponse> SendOnceAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);

        var timestamp = _clock().ToUnixTimeSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            ShopeeSignatureBuilder.BuildAuthorizationHeader(_options.AppId, timestamp, payload, _options.Secret));
        request.Headers.UserAgent.ParseAdd(ShopeeAffiliateDefaults.UserAgent);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            using var httpResponse = await _httpClient.SendAsync(request, timeoutCts.Token);
            var text = await httpResponse.Content.ReadAsStringAsync(timeoutCts.Token);
            var requestId = TryGetHeader(httpResponse, "X-Request-ID")
                            ?? TryGetHeader(httpResponse, "X-Tt-Logid");

            ThrowForUnsuccessfulStatus(httpResponse.StatusCode, text, requestId);

            var body = ParseJson(text);
            try
            {
                ShopeeReportsErrorMapper.ThrowIfError(body.RootElement, requestId);
            }
            catch
            {
                body.Dispose();
                throw;
            }
            return new ShopeeAffiliateGraphQlResponse(body, text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeAffiliateApiException(
                $"Shopee API request timed out after {_options.Timeout.TotalMilliseconds:0}ms.",
                code: "HTTP_TIMEOUT");
        }
    }

    private static void ThrowForUnsuccessfulStatus(HttpStatusCode statusCode, string body, string? requestId)
    {
        if ((int)statusCode >= 500)
        {
            throw new ShopeeAffiliateApiException(
                $"Shopee API HTTP {(int)statusCode}: {Truncate(body, 500)}",
                code: "HTTP_5XX",
                requestId: requestId);
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ShopeeAffiliateRateLimitException(
                $"Shopee API rate limited (HTTP {(int)statusCode}).",
                code: "HTTP_429",
                requestId: requestId);
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ShopeeAffiliateAuthException(
                $"Shopee API authentication failed (HTTP {(int)statusCode}).",
                code: "HTTP_" + ((int)statusCode).ToString(),
                requestId: requestId);
        }

        if ((int)statusCode >= 400)
        {
            throw new ShopeeAffiliateApiException(
                $"Shopee API HTTP {(int)statusCode}: {Truncate(body, 500)}",
                code: "HTTP_4XX",
                requestId: requestId);
        }
    }

    private static JsonDocument ParseJson(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text)
                ? JsonDocument.Parse("{}")
                : JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new ShopeeAffiliateApiException(
                $"Shopee API returned non-JSON response: {Truncate(text, 500)}",
                code: "INVALID_JSON",
                innerException: ex);
        }
    }

    private static string? TryGetHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static int NormalizePageSize(int requested)
    {
        if (requested <= 0) return DefaultPageSize;
        return Math.Min(requested, MaxPageSize);
    }

    private static void EnsureWindow(DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from)
        {
            throw new ArgumentException("The 'To' value must be greater than or equal to 'From'.", nameof(to));
        }
    }

    private static string MaskAppId(string appId)
    {
        if (string.IsNullOrEmpty(appId)) return string.Empty;
        if (appId.Length <= 4) return new string('*', appId.Length);
        return $"{appId[..2]}***{appId[^2..]}";
    }
}
