using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shopee.Affiliate.Domain;
using Shopee.Affiliate.Infrastructure;

namespace Shopee.Affiliate.Application;

public sealed class ShopeeAffiliateClient : IShopeeAffiliateClient
{
    private readonly HttpClient _httpClient;
    private readonly ShopeeAffiliateOptions _options;
    private readonly IShopeeAffiliateGraphQlTransport _graphQlTransport;

    [ActivatorUtilitiesConstructor]
    public ShopeeAffiliateClient(HttpClient httpClient, IOptions<ShopeeAffiliateOptions> options)
        : this(httpClient, options?.Value ?? throw new ArgumentNullException(nameof(options)))
    {
    }

    public ShopeeAffiliateClient(
        HttpClient httpClient,
        ShopeeAffiliateOptions options,
        Func<long>? nowSeconds = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        var clock = nowSeconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _graphQlTransport = new ShopeeAffiliateGraphQlTransport(_httpClient, clock);
    }

    public async Task<ShopeeAffiliateLinkResult> GenerateAffiliateLinkAsync(
        ShopeeAffiliateLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _options.Validate();
        EnsureValidOriginUrl(request.OriginUrl);

        using var timeoutCts = CreateTimeoutTokenSource(cancellationToken);
        var token = timeoutCts.Token;
        var resolvedUrl = await ResolveOriginUrlIfNeededAsync(request.OriginUrl, request.ResolveShortUrls, token);

        if (request.Strategy is ShopeeAffiliateLinkStrategy.ShortLinkOnly)
        {
            var shortLink = await GenerateShortLinkCoreAsync(request.OriginUrl, request.SubIds, token);
            return CreateShortLinkResult(shortLink.ShortLink, resolvedUrl);
        }

        var productOffer = await GetProductOfferForLinkStrategyAsync(request, resolvedUrl, token);
        if (productOffer?.AffiliateUrl is not null)
        {
            return new ShopeeAffiliateLinkResult
            {
                AffiliateUrl = productOffer.AffiliateUrl,
                Source = ShopeeAffiliateLinkSource.ProductOffer,
                ResolvedOriginUrl = resolvedUrl,
                Product = productOffer
            };
        }

        if (request.Strategy is ShopeeAffiliateLinkStrategy.ProductOfferOnly)
        {
            throw new ShopeeAffiliateApiException("Shopee API did not return a product offer link.");
        }

        var fallbackShortLink = await GenerateShortLinkCoreAsync(request.OriginUrl, request.SubIds, token);
        return CreateShortLinkResult(fallbackShortLink.ShortLink, resolvedUrl);
    }

    public async Task<ShopeeShortLinkResult> GenerateShortLinkAsync(
        ShopeeShortLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _options.Validate();
        EnsureValidOriginUrl(request.OriginUrl);

        using var timeoutCts = CreateTimeoutTokenSource(cancellationToken);
        return await GenerateShortLinkCoreAsync(request.OriginUrl, request.SubIds, timeoutCts.Token);
    }

    public async Task<ShopeeProductOffer?> GetProductOfferAsync(
        ShopeeProductOfferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _options.Validate();

        using var timeoutCts = CreateTimeoutTokenSource(cancellationToken);
        return await GetProductOfferCoreAsync(request.ProductIdentity, timeoutCts.Token);
    }

    public async Task<Uri> ResolveShopeeUrlAsync(
        Uri originUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureValidOriginUrl(originUrl);

        using var timeoutCts = CreateTimeoutTokenSource(cancellationToken);
        return await ResolveShopeeUrlCoreAsync(originUrl, timeoutCts.Token);
    }

    private async Task<ShopeeShortLinkResult> GenerateShortLinkCoreAsync(
        Uri originUrl,
        IReadOnlyList<string> requestSubIds,
        CancellationToken cancellationToken)
    {
        var payload = ShopeeAffiliateGraphQlPayloadFactory.BuildGenerateShortLinkPayload(
            originUrl.ToString(),
            ResolveSubIds(requestSubIds));

        using var response = await _graphQlTransport.PostAsync(payload, _options, cancellationToken);
        var shortLink = ShopeeAffiliateResponseMapper.ExtractShortLink(response.Body.RootElement);

        return new ShopeeShortLinkResult
        {
            ShortLink = shortLink,
            RawResponse = response.RawBody
        };
    }

    private async Task<ShopeeProductOffer?> GetProductOfferCoreAsync(
        ShopeeAffiliateProductIdentity productIdentity,
        CancellationToken cancellationToken)
    {
        var payload = ShopeeAffiliateGraphQlPayloadFactory.BuildProductOfferPayload(productIdentity);

        using var response = await _graphQlTransport.PostAsync(payload, _options, cancellationToken);
        return ShopeeAffiliateResponseMapper.ExtractProductOffer(response.Body.RootElement, _options.PriceCulture);
    }

    private async Task<Uri> ResolveShopeeUrlCoreAsync(
        Uri originUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, originUrl);
        request.Headers.UserAgent.ParseAdd(ShopeeAffiliateDefaults.UserAgent);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return response.RequestMessage?.RequestUri is { } finalUrl &&
                   ShopeeAffiliateUrlParser.IsValidHttpUrl(finalUrl.ToString())
                ? finalUrl
                : originUrl;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeAffiliateApiException($"Shopee URL resolve timed out after {_options.Timeout.TotalMilliseconds:0}ms.");
        }
        catch
        {
            return originUrl;
        }
    }

    private async Task<Uri> ResolveOriginUrlIfNeededAsync(
        Uri originUrl,
        bool shouldResolveShortUrls,
        CancellationToken cancellationToken)
    {
        return shouldResolveShortUrls
            ? await ResolveShopeeUrlCoreAsync(originUrl, cancellationToken)
            : originUrl;
    }

    private async Task<ShopeeProductOffer?> GetProductOfferForLinkStrategyAsync(
        ShopeeAffiliateLinkRequest request,
        Uri resolvedUrl,
        CancellationToken cancellationToken)
    {
        var productIdentity = TryFindProductIdentity(resolvedUrl, request.OriginUrl);
        if (productIdentity is null)
        {
            return null;
        }

        try
        {
            return await GetProductOfferCoreAsync(productIdentity, cancellationToken);
        }
        catch when (request.Strategy is ShopeeAffiliateLinkStrategy.PreferProductOffer)
        {
            return null;
        }
    }

    private IReadOnlyList<string> ResolveSubIds(IReadOnlyList<string> requestSubIds)
        => requestSubIds.Count > 0 ? requestSubIds : _options.SubIds;

    private static ShopeeAffiliateProductIdentity? TryFindProductIdentity(
        Uri resolvedUrl,
        Uri originUrl)
    {
        return ShopeeAffiliateUrlParser.TryExtractProductIdentity(resolvedUrl.ToString(), out var resolvedIdentity) ? resolvedIdentity :
            ShopeeAffiliateUrlParser.TryExtractProductIdentity(originUrl.ToString(), out var originIdentity) ? originIdentity :
            null;
    }

    private static void EnsureValidOriginUrl(Uri originUrl)
    {
        if (originUrl is null ||
            (originUrl.Scheme != Uri.UriSchemeHttp && originUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A valid Shopee origin URL is required.", nameof(originUrl));
        }
    }

    private CancellationTokenSource CreateTimeoutTokenSource(CancellationToken cancellationToken)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);
        return timeoutCts;
    }

    private static ShopeeAffiliateLinkResult CreateShortLinkResult(
        Uri shortLink,
        Uri resolvedUrl)
    {
        return new ShopeeAffiliateLinkResult
        {
            AffiliateUrl = shortLink,
            Source = ShopeeAffiliateLinkSource.ShortLink,
            ResolvedOriginUrl = resolvedUrl
        };
    }
}
