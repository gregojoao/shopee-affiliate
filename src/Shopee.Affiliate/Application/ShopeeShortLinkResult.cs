using System.Text.Json;

namespace Shopee.Affiliate;

public sealed record ShopeeShortLinkResult(
    string ShortLink,
    string Payload,
    JsonDocument ResponseBody) : IDisposable
{
    public void Dispose() => ResponseBody.Dispose();
}
