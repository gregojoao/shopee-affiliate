using System.Text;
using System.Text.Json;

namespace Shopee.Affiliate;

public sealed class ShopeeAffiliateClient
{
    private readonly HttpClient _httpClient;
    private readonly Func<long> _nowSeconds;

    public ShopeeAffiliateClient(HttpClient httpClient, Func<long>? nowSeconds = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _nowSeconds = nowSeconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public async Task<ShopeeAffiliateLinkResult> GenerateAffiliateLinkAsync(
        string originUrl,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        EnsureValidOriginUrl(originUrl);

        using var timeoutCts = CreateTimeoutTokenSource(options, cancellationToken);
        var token = timeoutCts.Token;
        var resolvedUrl = options.ResolveShortUrls
            ? await ResolveShopeeUrlAsync(originUrl, options, token)
            : originUrl;

        ShopeeProductOffer? productOffer = null;
        var productIdentity =
            ShopeeAffiliateUrlParser.TryExtractProductIdentity(resolvedUrl, out var resolvedIdentity) ? resolvedIdentity :
            ShopeeAffiliateUrlParser.TryExtractProductIdentity(originUrl, out var originIdentity) ? originIdentity :
            null;

        if (options.PreferProductOffer && productIdentity is not null)
        {
            try
            {
                using var productOfferResult = await GetProductOfferAsync(productIdentity, options, token);
                productOffer = productOfferResult.ProductOffer;

                if (!string.IsNullOrWhiteSpace(productOffer?.AffiliateUrl))
                {
                    return CreateAffiliateResult(productOffer.AffiliateUrl, originUrl, resolvedUrl, productOffer);
                }
            }
            catch when (options.FallbackToShortLink)
            {
                productOffer = null;
            }
        }

        if (!options.FallbackToShortLink)
        {
            throw new ShopeeAffiliateApiException("Shopee API did not return a product offer link.");
        }

        using var shortLinkResult = await GenerateShortLinkAsync(originUrl, options, token);
        return CreateAffiliateResult(shortLinkResult.ShortLink, originUrl, resolvedUrl, productOffer);
    }

    public async Task<ShopeeShortLinkResult> GenerateShortLinkAsync(
        string originUrl,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        EnsureValidOriginUrl(originUrl);

        using var timeoutCts = CreateTimeoutTokenSource(options, cancellationToken);
        var payload = ShopeeAffiliateGraphQlPayloadFactory.BuildGenerateShortLinkPayload(originUrl, options.SubIds);
        var responseBody = await PostGraphQLAsync(payload, options, timeoutCts.Token);
        var shortLink = ShopeeAffiliateResponseMapper.ExtractShortLink(responseBody.RootElement);

        return new ShopeeShortLinkResult(shortLink, payload, responseBody);
    }

    public async Task<ShopeeProductOfferResult> GetProductOfferAsync(
        ShopeeAffiliateProductIdentity productIdentity,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        using var timeoutCts = CreateTimeoutTokenSource(options, cancellationToken);
        var payload = ShopeeAffiliateGraphQlPayloadFactory.BuildProductOfferPayload(productIdentity);
        var responseBody = await PostGraphQLAsync(payload, options, timeoutCts.Token);
        var productOffer = ShopeeAffiliateResponseMapper.ExtractProductOffer(responseBody.RootElement, options.PriceCultureName);

        return new ShopeeProductOfferResult(productOffer, payload, responseBody);
    }

    public async Task<string> ResolveShopeeUrlAsync(
        string originUrl,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidOriginUrl(originUrl);

        using var timeoutCts = CreateTimeoutTokenSource(options, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, originUrl);
        request.Headers.UserAgent.ParseAdd("Shopee.Affiliate/0.2");

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            return response.RequestMessage?.RequestUri?.ToString() is { Length: > 0 } finalUrl &&
                   ShopeeAffiliateUrlParser.IsValidHttpUrl(finalUrl)
                ? finalUrl
                : originUrl;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeAffiliateApiException($"Shopee URL resolve timed out after {options.TimeoutMilliseconds}ms.");
        }
        catch
        {
            return originUrl;
        }
    }

    public static string BuildGenerateShortLinkPayload(string originUrl, IEnumerable<string>? subIds = null)
        => ShopeeAffiliateGraphQlPayloadFactory.BuildGenerateShortLinkPayload(originUrl, subIds);

    public static string BuildProductOfferPayload(ShopeeAffiliateProductIdentity productIdentity)
        => ShopeeAffiliateGraphQlPayloadFactory.BuildProductOfferPayload(productIdentity);

    public static string BuildAuthorizationHeader(string appId, long timestamp, string payload, string secret)
        => ShopeeAffiliateAuthenticator.BuildAuthorizationHeader(appId, timestamp, payload, secret);

    public static string CreateSignature(string appId, long timestamp, string payload, string secret)
        => ShopeeAffiliateAuthenticator.CreateSignature(appId, timestamp, payload, secret);

    public static string ExtractShortLink(JsonElement responseBody)
        => ShopeeAffiliateResponseMapper.ExtractShortLink(responseBody);

    public static ShopeeProductOffer? ExtractProductOffer(JsonElement responseBody, string priceCultureName = "pt-BR")
        => ShopeeAffiliateResponseMapper.ExtractProductOffer(responseBody, priceCultureName);

    public static bool TryExtractProductIdentity(string value, out ShopeeAffiliateProductIdentity productIdentity)
        => ShopeeAffiliateUrlParser.TryExtractProductIdentity(value, out productIdentity);

    public static IReadOnlyList<string> ReadSubIdsFromEnvironment(string? value)
        => ShopeeAffiliateGraphQlPayloadFactory.ReadSubIdsFromEnvironment(value);

    public static string FormatShopeePriceRange(string? priceMin, string? priceMax, string priceCultureName = "pt-BR")
        => ShopeePriceFormatter.FormatShopeePriceRange(priceMin, priceMax, priceCultureName);

    public static string ComputeShopeeOriginalPrice(
        string? priceMin,
        string? priceMax,
        string? priceDiscountRate,
        string priceCultureName = "pt-BR")
        => ShopeePriceFormatter.ComputeShopeeOriginalPrice(priceMin, priceMax, priceDiscountRate, priceCultureName);

    private async Task<JsonDocument> PostGraphQLAsync(
        string payload,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken)
    {
        var timestamp = _nowSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            ShopeeAffiliateAuthenticator.BuildAuthorizationHeader(options.AppId, timestamp, payload, options.Secret));
        request.Headers.UserAgent.ParseAdd("Shopee.Affiliate/0.2");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);

            JsonDocument responseBody;
            try
            {
                responseBody = string.IsNullOrWhiteSpace(text)
                    ? JsonDocument.Parse("{}")
                    : JsonDocument.Parse(text);
            }
            catch (JsonException ex)
            {
                throw new ShopeeAffiliateApiException(
                    $"Shopee API returned non-JSON response: {Truncate(text, 500)}",
                    ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                using (responseBody)
                {
                    throw new ShopeeAffiliateApiException(
                        $"Shopee API HTTP {(int)response.StatusCode}: {Truncate(text, 1000)}");
                }
            }

            return responseBody;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeAffiliateApiException($"Shopee API request timed out after {options.TimeoutMilliseconds}ms.");
        }
    }

    private static void EnsureValidOriginUrl(string originUrl)
    {
        if (!ShopeeAffiliateUrlParser.IsValidHttpUrl(originUrl))
        {
            throw new ArgumentException("A valid Shopee origin URL is required.", nameof(originUrl));
        }
    }

    private static CancellationTokenSource CreateTimeoutTokenSource(
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(options.TimeoutMilliseconds, 1)));
        return timeoutCts;
    }

    private static ShopeeAffiliateLinkResult CreateAffiliateResult(
        string affiliateUrl,
        string originUrl,
        string resolvedUrl,
        ShopeeProductOffer? productOffer)
    {
        return new ShopeeAffiliateLinkResult(
            AffiliateUrl: affiliateUrl,
            ShortLink: affiliateUrl,
            ProductTitle: productOffer?.ProductTitle ?? string.Empty,
            ProductPrice: productOffer?.ProductPrice ?? string.Empty,
            ProductOriginalPrice: productOffer?.ProductOriginalPrice ?? string.Empty,
            ProductImageUrl: productOffer?.ProductImageUrl ?? productOffer?.ImageUrl ?? string.Empty,
            ProductUrl: productOffer?.ProductUrl ?? resolvedUrl ?? originUrl,
            FinalProductUrl: productOffer?.ProductUrl ?? resolvedUrl ?? originUrl,
            Platform: "Shopee",
            ResolvedUrl: resolvedUrl ?? string.Empty,
            ProductOffer: productOffer);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
