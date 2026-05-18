using Shopee.Affiliate.Signing;

namespace Shopee.Affiliate.Infrastructure;

internal static class ShopeeAffiliateAuthenticator
{
    public static string BuildAuthorizationHeader(
        string appId,
        long timestamp,
        string payload,
        string secret)
        => ShopeeSignatureBuilder.BuildAuthorizationHeader(appId, timestamp, payload, secret);

    public static string CreateSignature(
        string appId,
        long timestamp,
        string payload,
        string secret)
        => ShopeeSignatureBuilder.CreateSignature(appId, timestamp, payload, secret);
}
