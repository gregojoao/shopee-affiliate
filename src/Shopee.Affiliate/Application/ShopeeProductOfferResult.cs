using System.Text.Json;

namespace Shopee.Affiliate;

public sealed record ShopeeProductOfferResult(
    ShopeeProductOffer? ProductOffer,
    string Payload,
    JsonDocument ResponseBody) : IDisposable
{
    public void Dispose() => ResponseBody.Dispose();
}
