using Shopee.Affiliate.Domain;

namespace Shopee.Affiliate.Application;

public sealed record ShopeeProductOfferRequest
{
    public required ShopeeAffiliateProductIdentity ProductIdentity { get; init; }
}
