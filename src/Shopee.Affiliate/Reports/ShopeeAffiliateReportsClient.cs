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

    public ShopeeAffiliateReportsClient(HttpClient httpClient, Func<DateTimeOffset> clock)
        : this(httpClient, ReadOptionsFromEnv(), clock, logger: null)
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
            cursor = await ScrollToPageAsync(request, status, pageSize, page, cancellationToken).ConfigureAwait(false);
        }

        var payload = ShopeeReportsGraphQlPayloadFactory.BuildListConversionsPayload(
            request.From.ToUnixTimeSeconds(),
            request.To.ToUnixTimeSeconds(),
            status,
            request.SubId,
            cursor,
            pageSize);

        using var response = await SendAsync(payload, cancellationToken).ConfigureAwait(false);
        var mapped = ShopeeReportsResponseMapper.MapConversionPage(response.Body.RootElement, page, pageSize);
        return string.IsNullOrWhiteSpace(request.SubId) ? mapped : FilterPageBySubId(mapped, request.SubId!);
    }

    private static ShopeeConversionPage FilterPageBySubId(ShopeeConversionPage page, string subId)
    {
        var filtered = page.Items
            .Where(c => c.SubIds.Any(s => string.Equals(s, subId, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return page with { Items = filtered };
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
        using var response = await SendAsync(payload, cancellationToken).ConfigureAwait(false);
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
        var safetyPages = 0;
        const int MaxSafetyPages = 200;

        do
        {
            var payload = ShopeeReportsGraphQlPayloadFactory.BuildListConversionsPayload(
                request.From.ToUnixTimeSeconds(),
                request.To.ToUnixTimeSeconds(),
                "ALL",
                request.SubId,
                cursor,
                limit: 500);

            using var response = await SendAsync(payload, cancellationToken).ConfigureAwait(false);
            var page = ShopeeReportsResponseMapper.MapConversionPage(response.Body.RootElement, requestedPage: 1, requestedLimit: 500);
            var pageItems = string.IsNullOrWhiteSpace(request.SubId)
                ? page.Items
                : page.Items.Where(c => c.SubIds.Any(s => string.Equals(s, request.SubId, StringComparison.OrdinalIgnoreCase))).ToList();
            conversions.AddRange(pageItems);

            if (!page.HasMore || string.IsNullOrEmpty(page.NextCursor)) break;
            cursor = page.NextCursor;
            safetyPages++;
        } while (safetyPages < MaxSafetyPages);

        return AggregateSummary(conversions, request.From, request.To);
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
            var payload = ShopeeReportsGraphQlPayloadFactory.BuildListConversionsPayload(
                request.From.ToUnixTimeSeconds(),
                request.To.ToUnixTimeSeconds(),
                status,
                request.SubId,
                cursor,
                pageSize);

            using var response = await SendAsync(payload, cancellationToken).ConfigureAwait(false);
            var page = ShopeeReportsResponseMapper.MapConversionPage(response.Body.RootElement, current, pageSize);
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
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await SendOnceAsync(payload, cancellationToken).ConfigureAwait(false);
            }
            catch (ShopeeAffiliateApiException ex) when (ex.Code is "HTTP_5XX" or "HTTP_TIMEOUT" && attempt == 1)
            {
                _logger.LogWarning(
                    "Shopee reports request failed (code {Code}); retrying once. AppId={AppId}",
                    ex.Code, MaskAppId(_options.AppId));
            }
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

        HttpResponseMessage? httpResponse = null;
        try
        {
            httpResponse = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            var text = await httpResponse.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            var requestId = TryGetHeader(httpResponse, "X-Request-ID")
                            ?? TryGetHeader(httpResponse, "X-Tt-Logid");

            if ((int)httpResponse.StatusCode >= 500)
            {
                httpResponse.Dispose();
                throw new ShopeeAffiliateApiException(
                    $"Shopee API HTTP {(int)httpResponse.StatusCode}: {Truncate(text, 500)}",
                    code: "HTTP_5XX");
            }

            if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                httpResponse.Dispose();
                throw new ShopeeAffiliateRateLimitException(
                    $"Shopee API rate limited (HTTP {(int)httpResponse.StatusCode}).",
                    code: "HTTP_429",
                    requestId: requestId);
            }

            if (httpResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                httpResponse.Dispose();
                throw new ShopeeAffiliateAuthException(
                    $"Shopee API authentication failed (HTTP {(int)httpResponse.StatusCode}).",
                    code: "HTTP_" + ((int)httpResponse.StatusCode).ToString(),
                    requestId: requestId);
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                httpResponse.Dispose();
                throw new ShopeeAffiliateApiException(
                    $"Shopee API HTTP {(int)httpResponse.StatusCode}: {Truncate(text, 500)}",
                    code: "HTTP_4XX");
            }

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

            httpResponse.Dispose();
            return new ShopeeAffiliateGraphQlResponse(body, text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            httpResponse?.Dispose();
            throw new ShopeeAffiliateApiException(
                $"Shopee API request timed out after {_options.Timeout.TotalMilliseconds:0}ms.",
                code: "HTTP_TIMEOUT");
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
        if (requested <= 0) return 50;
        return Math.Min(requested, 500);
    }

    private static void EnsureWindow(DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from)
        {
            throw new ArgumentException("The 'To' value must be greater than or equal to 'From'.", nameof(to));
        }
    }

    private static ShopeeSalesSummary AggregateSummary(
        IReadOnlyList<ShopeeConversion> conversions,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        if (conversions.Count == 0)
        {
            return new ShopeeSalesSummary(
                PeriodStart: from,
                PeriodEnd: to,
                Conversions: 0,
                Clicks: null,
                GrossRevenue: Money.Zero(),
                Commission: Money.Zero(),
                AvgCommissionRate: 0m,
                ConversionRate: null,
                ByStatus: new Dictionary<ShopeeOrderStatus, int>(),
                TopProducts: Array.Empty<ShopeeTopProduct>(),
                TopShops: Array.Empty<ShopeeTopShop>(),
                TopSubIds: Array.Empty<ShopeeTopSubId>(),
                Supported: true,
                UnsupportedReason: null);
        }

        var currency = conversions[0].Currency ?? "BRL";
        var gross = 0m;
        var commission = 0m;
        var rateSum = 0m;
        var rateCount = 0;
        var byStatus = new Dictionary<ShopeeOrderStatus, int>();
        var productAgg = new Dictionary<string, (string? Title, int Conversions, decimal Commission)>();
        var shopAgg = new Dictionary<string, (string? Name, int Conversions, decimal Commission)>();
        var subAgg = new Dictionary<string, (int Conversions, decimal Commission)>();

        foreach (var c in conversions)
        {
            gross += c.TotalSale.Amount;
            commission += c.Commission.Amount;
            if (c.CommissionRate > 0)
            {
                rateSum += c.CommissionRate;
                rateCount++;
            }

            byStatus[c.Status] = byStatus.TryGetValue(c.Status, out var current) ? current + 1 : 1;

            if (!string.IsNullOrWhiteSpace(c.ProductId))
            {
                var prev = productAgg.TryGetValue(c.ProductId!, out var p)
                    ? p
                    : (c.ProductTitle, 0, 0m);
                productAgg[c.ProductId!] = (prev.Item1 ?? c.ProductTitle, prev.Item2 + 1, prev.Item3 + c.Commission.Amount);
            }

            if (!string.IsNullOrWhiteSpace(c.ShopId))
            {
                var prev = shopAgg.TryGetValue(c.ShopId!, out var s)
                    ? s
                    : (c.ShopName, 0, 0m);
                shopAgg[c.ShopId!] = (prev.Item1 ?? c.ShopName, prev.Item2 + 1, prev.Item3 + c.Commission.Amount);
            }

            foreach (var sub in c.SubIds)
            {
                var prev = subAgg.TryGetValue(sub, out var s) ? s : (0, 0m);
                subAgg[sub] = (prev.Item1 + 1, prev.Item2 + c.Commission.Amount);
            }
        }

        var topProducts = productAgg
            .OrderByDescending(p => p.Value.Commission)
            .Take(10)
            .Select(p => new ShopeeTopProduct(p.Key, p.Value.Title, p.Value.Conversions, new Money(p.Value.Commission, currency)))
            .ToList();

        var topShops = shopAgg
            .OrderByDescending(p => p.Value.Commission)
            .Take(10)
            .Select(p => new ShopeeTopShop(p.Key, p.Value.Name, p.Value.Conversions, new Money(p.Value.Commission, currency)))
            .ToList();

        var topSubIds = subAgg
            .OrderByDescending(p => p.Value.Commission)
            .Take(10)
            .Select(p => new ShopeeTopSubId(p.Key, p.Value.Conversions, new Money(p.Value.Commission, currency)))
            .ToList();

        var avgRate = rateCount > 0 ? Math.Round(rateSum / rateCount, 4, MidpointRounding.AwayFromZero) : 0m;

        return new ShopeeSalesSummary(
            PeriodStart: from,
            PeriodEnd: to,
            Conversions: conversions.Count,
            Clicks: null,
            GrossRevenue: new Money(gross, currency),
            Commission: new Money(commission, currency),
            AvgCommissionRate: avgRate,
            ConversionRate: null,
            ByStatus: byStatus,
            TopProducts: topProducts,
            TopShops: topShops,
            TopSubIds: topSubIds,
            Supported: true,
            UnsupportedReason: null);
    }

    private static string MaskAppId(string appId)
    {
        if (string.IsNullOrEmpty(appId)) return string.Empty;
        if (appId.Length <= 4) return new string('*', appId.Length);
        return $"{appId[..2]}***{appId[^2..]}";
    }

    private static ShopeeAffiliateReportsOptions ReadOptionsFromEnv()
    {
        return new ShopeeAffiliateReportsOptions
        {
            AppId = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_APP_ID") ?? string.Empty,
            Secret = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_SECRET") ?? string.Empty
        };
    }
}
