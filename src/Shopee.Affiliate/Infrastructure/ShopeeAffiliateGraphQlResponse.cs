using System.Text.Json;

namespace Shopee.Affiliate.Infrastructure;

internal sealed record ShopeeAffiliateGraphQlResponse(
    JsonDocument Body,
    string RawBody) : IDisposable
{
    public void Dispose() => Body.Dispose();
}
