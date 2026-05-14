using Shopee.Affiliate.Domain;

namespace Shopee.Affiliate.Application;

public sealed record ShopeeAffiliateLinkResult
{
    public required Uri AffiliateUrl { get; init; }

    public required ShopeeAffiliateLinkSource Source { get; init; }

    public Uri? ResolvedOriginUrl { get; init; }

    public ShopeeProductOffer? Product { get; init; }
}
