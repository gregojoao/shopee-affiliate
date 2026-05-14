namespace Shopee.Affiliate;

public interface IShopeeAffiliateService
{
    Task<ShopeeAffiliateLinkResult> GenerateAffiliateLinkAsync(
        string originUrl,
        CancellationToken cancellationToken = default);

    Task<ShopeeShortLinkResult> GenerateShortLinkAsync(
        string originUrl,
        CancellationToken cancellationToken = default);

    Task<ShopeeProductOfferResult> GetProductOfferAsync(
        ShopeeAffiliateProductIdentity productIdentity,
        CancellationToken cancellationToken = default);

    Task<string> ResolveShopeeUrlAsync(
        string originUrl,
        CancellationToken cancellationToken = default);
}
