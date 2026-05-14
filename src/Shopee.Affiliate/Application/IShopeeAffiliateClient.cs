using Shopee.Affiliate.Domain;

namespace Shopee.Affiliate.Application;

public interface IShopeeAffiliateClient
{
    Task<ShopeeAffiliateLinkResult> GenerateAffiliateLinkAsync(
        ShopeeAffiliateLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<ShopeeShortLinkResult> GenerateShortLinkAsync(
        ShopeeShortLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<ShopeeProductOffer?> GetProductOfferAsync(
        ShopeeProductOfferRequest request,
        CancellationToken cancellationToken = default);

    Task<Uri> ResolveShopeeUrlAsync(
        Uri originUrl,
        CancellationToken cancellationToken = default);
}
