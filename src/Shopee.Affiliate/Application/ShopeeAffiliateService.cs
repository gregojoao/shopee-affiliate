using Microsoft.Extensions.Options;

namespace Shopee.Affiliate;

public sealed class ShopeeAffiliateService(
    ShopeeAffiliateClient client,
    IOptions<ShopeeAffiliateOptions> options) : IShopeeAffiliateService
{
    private readonly ShopeeAffiliateClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly IOptions<ShopeeAffiliateOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

    public Task<ShopeeAffiliateLinkResult> GenerateAffiliateLinkAsync(
        string originUrl,
        CancellationToken cancellationToken = default)
    {
        return _client.GenerateAffiliateLinkAsync(originUrl, _options.Value, cancellationToken);
    }

    public Task<ShopeeShortLinkResult> GenerateShortLinkAsync(
        string originUrl,
        CancellationToken cancellationToken = default)
    {
        return _client.GenerateShortLinkAsync(originUrl, _options.Value, cancellationToken);
    }

    public Task<ShopeeProductOfferResult> GetProductOfferAsync(
        ShopeeAffiliateProductIdentity productIdentity,
        CancellationToken cancellationToken = default)
    {
        return _client.GetProductOfferAsync(productIdentity, _options.Value, cancellationToken);
    }

    public Task<string> ResolveShopeeUrlAsync(
        string originUrl,
        CancellationToken cancellationToken = default)
    {
        return _client.ResolveShopeeUrlAsync(originUrl, _options.Value, cancellationToken);
    }
}
