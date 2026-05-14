using Shopee.Affiliate.Application;

namespace Shopee.Affiliate.Infrastructure;

internal interface IShopeeAffiliateGraphQlTransport
{
    Task<ShopeeAffiliateGraphQlResponse> PostAsync(
        string payload,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken);
}
